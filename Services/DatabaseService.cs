using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using OptikFormApp.Models;
using System.Text.Json;

namespace OptikFormApp.Services
{
    /// <summary>
    /// SQLite veritabanı erişim katmanı.
    /// IDisposable ile tek bir connection paylaşılır; her metod
    /// bu connection üzerinde çalışır. Uygulama ömrü boyunca
    /// tek bir DatabaseService instance kullanılması önerilir.
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly string _dbPath;
        protected SqliteConnection _connection;
        private bool _disposed;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public DatabaseService(string? dbPath = null)
        {
            // Program Files dizinine yazma izni olmayabileceği için AppData'ya kaydet
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "OptikDegerlendirme");
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);
                dbPath = Path.Combine(appDataPath, "optik.db");
            }
            
            // Klasör yoksa oluştur
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _dbPath = dbPath;
            _connection = new SqliteConnection($"Data Source={_dbPath};Cache=Shared;Foreign Keys=True;");
            _connection.Open();
            
            // PRAGMA ayarlarını komutla yap
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA foreign_keys = ON;
            ";
            cmd.ExecuteNonQuery();
        }

        // ── Yardımcı ──────────────────────────────────────────────────────────

        private async Task EnableForeignKeysAsync()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            await cmd.ExecuteNonQueryAsync();
        }

        protected SqliteCommand CreateCommand(string sql)
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            return cmd;
        }

        public async Task InitializeDatabaseAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            await EnableForeignKeysAsync();
            using var cmd = CreateCommand(@"
                CREATE TABLE IF NOT EXISTS Courses (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT,
                    Name TEXT
                );

                CREATE TABLE IF NOT EXISTS Exams (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CourseId INTEGER,
                    Title TEXT,
                    ExamDate TEXT,
                    ConfigJson TEXT,
                    FOREIGN KEY(CourseId) REFERENCES Courses(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ExamResults (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ExamId INTEGER,
                    StudentId TEXT,
                    FullName TEXT,
                    BookletType TEXT,
                    RawAnswers TEXT,
                    Score REAL,
                    CorrectCount INTEGER,
                    IncorrectCount INTEGER,
                    EmptyCount INTEGER,
                    QuestionResultsJson TEXT,
                    FOREIGN KEY(ExamId) REFERENCES Exams(Id) ON DELETE CASCADE
                );");

            // Create indexes for performance optimization
            await cmd.ExecuteNonQueryAsync();
            
            // Performance indexes
            using var indexCmd = CreateCommand(@"
                CREATE INDEX IF NOT EXISTS idx_examresults_examid ON ExamResults(ExamId);
                CREATE INDEX IF NOT EXISTS idx_examresults_studentid ON ExamResults(StudentId);
                CREATE INDEX IF NOT EXISTS idx_examresults_score ON ExamResults(Score);
                CREATE INDEX IF NOT EXISTS idx_examresults_exam_student ON ExamResults(ExamId, StudentId);
                CREATE INDEX IF NOT EXISTS idx_courses_name ON Courses(Name);
                CREATE INDEX IF NOT EXISTS idx_exams_courseid ON Exams(CourseId);
                CREATE INDEX IF NOT EXISTS idx_exams_title ON Exams(Title);");
            await indexCmd.ExecuteNonQueryAsync();
        }

        // ── Courses ───────────────────────────────────────────────────────────

        public async Task<List<Course>> GetCoursesAsync()
        {
            var courses = new List<Course>();
            using var cmd = CreateCommand("SELECT Id, Code, Name FROM Courses ORDER BY Name");
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                courses.Add(new Course
                {
                    Id = reader.GetInt32(0),
                    Code = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Name = reader.GetString(2)
                });
            }
            return courses;
        }

        public async Task<int> SaveCourseAsync(Course course)
        {
            if (course.Id == 0)
            {
                using var cmd = CreateCommand("INSERT INTO Courses (Code, Name) VALUES (@code, @name); SELECT last_insert_rowid();");
                cmd.Parameters.AddWithValue("@code", course.Code);
                cmd.Parameters.AddWithValue("@name", course.Name);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            else
            {
                using var cmd = CreateCommand("UPDATE Courses SET Code=@code, Name=@name WHERE Id=@id");
                cmd.Parameters.AddWithValue("@code", course.Code);
                cmd.Parameters.AddWithValue("@name", course.Name);
                cmd.Parameters.AddWithValue("@id", course.Id);
                await cmd.ExecuteNonQueryAsync();
                return course.Id;
            }
        }

        public async Task DeleteCourseAsync(int courseId)
        {
            using var cmd = CreateCommand("DELETE FROM Courses WHERE Id = @id");
            cmd.Parameters.AddWithValue("@id", courseId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task RenameCourseAsync(int courseId, string newCode, string newName)
        {
            using var cmd = CreateCommand("UPDATE Courses SET Code = @code, Name = @name WHERE Id = @id");
            cmd.Parameters.AddWithValue("@code", newCode);
            cmd.Parameters.AddWithValue("@name", newName);
            cmd.Parameters.AddWithValue("@id", courseId);
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Exams ─────────────────────────────────────────────────────────────

        public async Task RenameExamAsync(int examId, string newTitle)
        {
            using var cmd = CreateCommand("UPDATE Exams SET Title = @title WHERE Id = @id");
            cmd.Parameters.AddWithValue("@title", newTitle);
            cmd.Parameters.AddWithValue("@id", examId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<ExamEntry>> GetExamsForCourseAsync(int courseId)
        {
            var exams = new List<ExamEntry>();
            using var cmd = CreateCommand("SELECT Id, CourseId, Title, ExamDate, ConfigJson FROM Exams WHERE CourseId = @cid ORDER BY ExamDate DESC");
            cmd.Parameters.AddWithValue("@cid", courseId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                exams.Add(new ExamEntry
                {
                    Id = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    Title = reader.GetString(2),
                    Date = DateTime.Parse(reader.GetString(3)),
                    ConfigJson = reader.GetString(4)
                });
            }
            return exams;
        }

        public async Task<int> SaveExamAsync(ExamEntry exam, List<StudentResult> results, System.Threading.CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync();
            try
            {
                using var transaction = await _connection.BeginTransactionAsync();
                try
                {
                    using var cmd = CreateCommand(
                        "INSERT INTO Exams (CourseId, Title, ExamDate, ConfigJson) VALUES (@cid, @title, @date, @config); SELECT last_insert_rowid();");
                    cmd.Transaction = (SqliteTransaction)transaction;
                    cmd.Parameters.AddWithValue("@cid", exam.CourseId);
                    cmd.Parameters.AddWithValue("@title", exam.Title);
                    cmd.Parameters.AddWithValue("@date", exam.Date.ToString("o"));
                    cmd.Parameters.AddWithValue("@config", exam.ConfigJson);
                    int examId = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));

                    await InsertResultsAsync((SqliteTransaction)transaction, examId, results, cancellationToken);
                    await transaction.CommitAsync();
                    return examId;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task UpdateExamAsync(ExamEntry exam, List<StudentResult> results, System.Threading.CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync();
            try
            {
                using var transaction = await _connection.BeginTransactionAsync();
                try
                {
                    using (var updateCmd = CreateCommand("UPDATE Exams SET Title=@title, ConfigJson=@config WHERE Id=@id"))
                    {
                        updateCmd.Transaction = (SqliteTransaction)transaction;
                        updateCmd.Parameters.AddWithValue("@title", exam.Title);
                        updateCmd.Parameters.AddWithValue("@config", exam.ConfigJson);
                        updateCmd.Parameters.AddWithValue("@id", exam.Id);
                        await updateCmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var delCmd = CreateCommand("DELETE FROM ExamResults WHERE ExamId = @eid"))
                    {
                        delCmd.Transaction = (SqliteTransaction)transaction;
                        delCmd.Parameters.AddWithValue("@eid", exam.Id);
                        await delCmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await InsertResultsAsync((SqliteTransaction)transaction, exam.Id, results, cancellationToken);
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task InsertResultsAsync(SqliteTransaction transaction, int examId, List<StudentResult> results, System.Threading.CancellationToken cancellationToken = default)
        {
            const string sql = @"
                INSERT INTO ExamResults
                    (ExamId, StudentId, FullName, BookletType, RawAnswers, Score, CorrectCount, IncorrectCount, EmptyCount, QuestionResultsJson)
                VALUES
                    (@eid, @sid, @name, @booklet, @raw, @score, @corr, @inc, @emp, @qjson)";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = transaction;

            var pEid = cmd.Parameters.Add("@eid", SqliteType.Integer);
            var pSid = cmd.Parameters.Add("@sid", SqliteType.Text);
            var pName = cmd.Parameters.Add("@name", SqliteType.Text);
            var pBooklet = cmd.Parameters.Add("@booklet", SqliteType.Text);
            var pRaw = cmd.Parameters.Add("@raw", SqliteType.Text);
            var pScore = cmd.Parameters.Add("@score", SqliteType.Real);
            var pCorr = cmd.Parameters.Add("@corr", SqliteType.Integer);
            var pInc = cmd.Parameters.Add("@inc", SqliteType.Integer);
            var pEmp = cmd.Parameters.Add("@emp", SqliteType.Integer);
            var pQjson = cmd.Parameters.Add("@qjson", SqliteType.Text);

            foreach (var res in results)
            {
                pEid.Value = examId;
                pSid.Value = (object?)res.StudentId ?? DBNull.Value;
                pName.Value = (object?)res.FullName ?? DBNull.Value;
                pBooklet.Value = (object?)res.BookletType ?? DBNull.Value;
                pRaw.Value = (object?)res.RawAnswers ?? DBNull.Value;
                pScore.Value = res.Score;
                pCorr.Value = res.CorrectCount;
                pInc.Value = res.IncorrectCount;
                pEmp.Value = res.EmptyCount;
                pQjson.Value = System.Text.Json.JsonSerializer.Serialize(res.QuestionResults);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        public async Task<List<StudentResult>> GetResultsForExamAsync(int examId)
        {
            var results = new List<StudentResult>();
            using var cmd = CreateCommand(@"
                SELECT StudentId, FullName, BookletType, RawAnswers, Score,
                       CorrectCount, IncorrectCount, EmptyCount, QuestionResultsJson
                FROM ExamResults WHERE ExamId = @eid");
            cmd.Parameters.AddWithValue("@eid", examId);
            using var reader = await cmd.ExecuteReaderAsync();
            int counter = 1;
            while (await reader.ReadAsync())
            {
                var res = new StudentResult
                {
                    RowNumber      = counter++,
                    StudentId      = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    FullName       = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    BookletType    = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    RawAnswers     = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Score          = reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4),
                    CorrectCount   = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    IncorrectCount = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    EmptyCount     = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                };

                int qNum = 1;
                foreach (char c in res.RawAnswers)
                {
                    res.Answers.Add(c.ToString());
                    res.ColoredAnswers.Add(new AnswerItem { Character = c == ' ' ? '_' : c, State = AnswerState.NotEvaluated, QuestionNumber = qNum++ });
                }

                try
                {
                    var qjson = reader.IsDBNull(8) ? "[]" : reader.GetString(8);
                    res.QuestionResults = System.Text.Json.JsonSerializer.Deserialize<List<bool>>(qjson) ?? new List<bool>();
                }
                catch
                {
                    res.QuestionResults = new List<bool>();
                }

                results.Add(res);
            }
            return results;
        }

        public async Task DeleteExamAsync(int examId)
        {
            using var cmd = CreateCommand("DELETE FROM Exams WHERE Id = @id");
            cmd.Parameters.AddWithValue("@id", examId);
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Bulk Operations ───────────────────────────────────────────────────────

        public async Task BulkInsertResultsAsync(int examId, List<StudentResult> results)
        {
            if (results == null || results.Count == 0) return;

            using var transaction = await _connection.BeginTransactionAsync();
            try
            {
                using var cmd = CreateCommand(@"
                    INSERT INTO ExamResults (ExamId, StudentId, FullName, BookletType, RawAnswers, Score, CorrectCount, IncorrectCount, EmptyCount, QuestionResultsJson)
                    VALUES (@eid, @sid, @name, @booklet, @raw, @score, @corr, @inc, @emp, @qjson)");
                
                cmd.Transaction = (SqliteTransaction)transaction;
                
                var pEid = cmd.Parameters.Add("@eid", SqliteType.Integer);
                var pSid = cmd.Parameters.Add("@sid", SqliteType.Text);
                var pName = cmd.Parameters.Add("@name", SqliteType.Text);
                var pBooklet = cmd.Parameters.Add("@booklet", SqliteType.Text);
                var pRaw = cmd.Parameters.Add("@raw", SqliteType.Text);
                var pScore = cmd.Parameters.Add("@score", SqliteType.Real);
                var pCorr = cmd.Parameters.Add("@corr", SqliteType.Integer);
                var pInc = cmd.Parameters.Add("@inc", SqliteType.Integer);
                var pEmp = cmd.Parameters.Add("@emp", SqliteType.Integer);
                var pQjson = cmd.Parameters.Add("@qjson", SqliteType.Text);

                foreach (var res in results)
                {
                    pEid.Value = examId;
                    pSid.Value = (object?)res.StudentId ?? DBNull.Value;
                    pName.Value = (object?)res.FullName ?? DBNull.Value;
                    pBooklet.Value = (object?)res.BookletType ?? DBNull.Value;
                    pRaw.Value = (object?)res.RawAnswers ?? DBNull.Value;
                    pScore.Value = res.Score;
                    pCorr.Value = res.CorrectCount;
                    pInc.Value = res.IncorrectCount;
                    pEmp.Value = res.EmptyCount;
                    pQjson.Value = System.Text.Json.JsonSerializer.Serialize(res.QuestionResults);
                    
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task BulkUpdateScoresAsync(List<(int examId, string studentId, double newScore)> updates)
        {
            if (updates == null || updates.Count == 0) return;

            using var transaction = await _connection.BeginTransactionAsync();
            try
            {
                using var cmd = CreateCommand(@"
                    UPDATE ExamResults 
                    SET Score = @score
                    WHERE ExamId = @eid AND StudentId = @sid");
                
                cmd.Transaction = (SqliteTransaction)transaction;
                
                var pEid = cmd.Parameters.Add("@eid", SqliteType.Integer);
                var pSid = cmd.Parameters.Add("@sid", SqliteType.Text);
                var pScore = cmd.Parameters.Add("@score", SqliteType.Real);

                foreach (var (examId, studentId, newScore) in updates)
                {
                    pEid.Value = examId;
                    pSid.Value = studentId;
                    pScore.Value = newScore;

                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task BulkDeleteResultsAsync(List<int> examIds)
        {
            if (examIds == null || examIds.Count == 0) return;

            await _gate.WaitAsync();
            try
            {
                using var transaction = await _connection.BeginTransactionAsync();
                try
                {
                    const int batchSize = 500;
                    for (int offset = 0; offset < examIds.Count; offset += batchSize)
                    {
                        var batch = examIds.Skip(offset).Take(batchSize).ToList();
                        using var cmd = _connection.CreateCommand();
                        cmd.Transaction = (SqliteTransaction)transaction;
                        var parameters = string.Join(",", batch.Select((_, i) => $"@eid{i}"));
                        cmd.CommandText = $"DELETE FROM ExamResults WHERE ExamId IN ({parameters})";
                        for (int i = 0; i < batch.Count; i++)
                        {
                            cmd.Parameters.AddWithValue($"@eid{i}", batch[i]);
                        }
                        await cmd.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _connection?.Close();
            _connection?.Dispose();
            _gate.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        // ── Backup & Restore Operations ─────────────────────────────────────────

        /// <summary>
        /// Veritabanı bağlantısını kapatır (restore işlemi için)
        /// </summary>
        public async Task CloseConnectionAsync()
        {
            await _gate.WaitAsync();
            try
            {
                if (_connection != null && _connection.State == System.Data.ConnectionState.Open)
                {
                    await _connection.CloseAsync();
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Veritabanı bağlantısını yeniden açar
        /// </summary>
        public async Task ReconnectAsync()
        {
            await _gate.WaitAsync();
            try
            {
                if (_connection != null)
                {
                    try { await _connection.CloseAsync(); } catch { }
                    try { _connection.Dispose(); } catch { }
                    _connection = null!;
                }

                _connection = new SqliteConnection($"Data Source={_dbPath};Cache=Shared;Foreign Keys=True;");
                await _connection.OpenAsync();

                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    PRAGMA journal_mode = WAL;
                    PRAGMA synchronous = NORMAL;
                    PRAGMA foreign_keys = ON;
                ";
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// SQLite backup komutu ile veritabanı yedeği alır
        /// </summary>
        public async Task BackupDatabaseAsync(string backupPath)
        {
            // Önce WAL dosyalarını temizle
            await _gate.WaitAsync();
            try
            {
                using var checkpointCmd = _connection.CreateCommand();
                checkpointCmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await checkpointCmd.ExecuteNonQueryAsync();

                using var backupConnection = new SqliteConnection($"Data Source={backupPath};");
                await backupConnection.OpenAsync();

                _connection.BackupDatabase(backupConnection);
                await backupConnection.CloseAsync();
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Veritabanı şema versiyonunu döner
        /// </summary>
        public async Task<int> GetUserVersionAsync()
        {
            using var cmd = CreateCommand("PRAGMA user_version;");
            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        /// <summary>
        /// Veritabanı şema versiyonunu ayarlar
        /// </summary>
        public async Task SetUserVersionAsync(int version)
        {
            if (version < 0 || version > 100000)
                throw new ArgumentOutOfRangeException(nameof(version), "Version 0..100000 aralığında olmalıdır.");

            // PRAGMA parametre bağlamayı desteklemez; değer doğrulanmış int olduğundan güvenlidir.
            using var cmd = CreateCommand($"PRAGMA user_version = {version};");
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
