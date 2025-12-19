using FluentAssertions;
using Scanner111.ViewModels;

namespace Scanner111.Tests;

public class ArticlesViewModelTests
{
    [Fact]
    public void Articles_ContainsNineLinks()
    {
        var viewModel = new ArticlesViewModel();
        
        viewModel.Articles.Should().HaveCount(9);
    }

    [Fact]
    public void Articles_ContainsExpectedUrls()
    {
        var viewModel = new ArticlesViewModel();
        
        viewModel.Articles.Should().Contain(a => a.Name == "BUFFOUT 4 INSTALLATION");
        viewModel.Articles.Should().Contain(a => a.Name == "CLASSIC GITHUB");
        viewModel.Articles.Should().Contain(a => a.Url.Contains("nexusmods.com"));
    }

    [Fact]
    public void OpenUrlCommand_IsNotNull()
    {
        var viewModel = new ArticlesViewModel();
        
        viewModel.OpenUrlCommand.Should().NotBeNull();
    }

    [Fact]
    public void OpenUrlCommand_CanExecute()
    {
        var viewModel = new ArticlesViewModel();
        
        viewModel.OpenUrlCommand.CanExecute.Subscribe(canExecute =>
        {
            canExecute.Should().BeTrue();
        });
    }
}
