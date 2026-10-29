using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class VersionCheckServiceTests
    {
        private class FakeHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _resp;
            public FakeHandler(HttpResponseMessage resp) { _resp = resp; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_resp);
        }

        [Fact]
        public async Task CheckForUpdate_NotFound_ReturnsError()
        {
            var resp = new HttpResponseMessage(HttpStatusCode.NotFound);
            var client = new HttpClient(new FakeHandler(resp));
            var svc = new VersionCheckService(client);
            var r = await svc.CheckForUpdateAsync("1.0.0");
            Assert.False(r.HasUpdate);
            Assert.Contains("bulunamad", r.ErrorMessage);
        }

        [Fact]
        public async Task CheckForUpdate_NewerRelease_HasUpdate()
        {
            var json = "{\"tag_name\":\"v2.0.0\",\"html_url\":\"u\",\"body\":\"notes\",\"published_at\":\"2025-01-01T00:00:00Z\",\"assets\":[]}";
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            var client = new HttpClient(new FakeHandler(resp));
            var svc = new VersionCheckService(client);
            var r = await svc.CheckForUpdateAsync("1.5.0");
            Assert.True(r.HasUpdate);
            Assert.Equal("2.0.0", r.LatestVersion);
        }
    }
}