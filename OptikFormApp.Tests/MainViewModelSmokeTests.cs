using OptikFormApp.Services;
using OptikFormApp.ViewModels;
using Xunit;

namespace OptikFormApp.Tests
{
    public class MainViewModelSmokeTests
    {
        [Fact]
        public void ServiceProvider_Resolves_MainViewModel()
        {
            var vm = ServiceProvider.GetService<MainViewModel>();
            Assert.NotNull(vm);
            Assert.NotNull(vm.CheckForUpdatesCommand);
        }
    }
}