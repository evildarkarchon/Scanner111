using FluentAssertions;
using Moq;
using Scanner111.Models;
using Scanner111.Services;
using Scanner111.ViewModels;
using System.Collections.ObjectModel;
using Xunit;

namespace Scanner111.Tests;

public class ResultsViewModelTests
{
    private readonly Mock<IScanResultsService> _scanResultsServiceMock;
    private readonly ObservableCollection<LogAnalysisResultDisplay> _results;

    public ResultsViewModelTests()
    {
        _results = new ObservableCollection<LogAnalysisResultDisplay>();
        _scanResultsServiceMock = new Mock<IScanResultsService>();
        _scanResultsServiceMock.Setup(s => s.Results).Returns(_results);
    }

    [Fact]
    public void RefreshCommand_IsNotNull()
    {
        var viewModel = new ResultsViewModel(_scanResultsServiceMock.Object);
        
        viewModel.RefreshCommand.Should().NotBeNull();
    }

    [Fact]
    public void DeleteSelectedCommand_IsNotNull()
    {
        var viewModel = new ResultsViewModel(_scanResultsServiceMock.Object);
        
        viewModel.DeleteSelectedCommand.Should().NotBeNull();
    }

    [Fact]
    public void SelectedResult_IsNullWhenNoResults()
    {
        var viewModel = new ResultsViewModel(_scanResultsServiceMock.Object);
        
        viewModel.SelectedResult.Should().BeNull();
    }

    [Fact]
    public void SelectedResult_IsFirstItemWhenResultsExist()
    {
        var result = new LogAnalysisResultDisplay { FileName = "test.log" };
        _results.Add(result);
        
        var viewModel = new ResultsViewModel(_scanResultsServiceMock.Object);
        
        viewModel.SelectedResult.Should().Be(result);
    }

    [Fact]
    public void DeleteSelectedCommand_RemovesSelectedItem()
    {
        var result1 = new LogAnalysisResultDisplay { FileName = "test1.log" };
        var result2 = new LogAnalysisResultDisplay { FileName = "test2.log" };
        _results.Add(result1);
        _results.Add(result2);
        
        var viewModel = new ResultsViewModel(_scanResultsServiceMock.Object);
        viewModel.SelectedResult = result1;
        
        viewModel.DeleteSelectedCommand.Execute().Subscribe();
        
        _results.Should().HaveCount(1);
        _results.Should().NotContain(result1);
        viewModel.SelectedResult.Should().Be(result2);
    }

    [Fact]
    public void RefreshCommand_SelectsFirstItem()
    {
        var result1 = new LogAnalysisResultDisplay { FileName = "test1.log" };
        var result2 = new LogAnalysisResultDisplay { FileName = "test2.log" };
        _results.Add(result1);
        _results.Add(result2);
        
        var viewModel = new ResultsViewModel(_scanResultsServiceMock.Object);
        viewModel.SelectedResult = result2;
        
        viewModel.RefreshCommand.Execute().Subscribe();
        
        viewModel.SelectedResult.Should().Be(result1);
    }
}
