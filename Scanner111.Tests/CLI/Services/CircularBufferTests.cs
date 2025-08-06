using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Scanner111.CLI.Services;
using Xunit;

namespace Scanner111.Tests.CLI.Services;

public class CircularBufferTests
{
    #region Basic Operations Tests

    [Fact]
    public void Add_ToEmptyBuffer_AddsItem()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        buffer.Add(1);

        // Assert
        var items = buffer.GetItems().ToList();
        Assert.Single(items);
        Assert.Equal(1, items[0]);
    }

    [Fact]
    public void Add_MultipleItems_MaintainsOrder()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(5);

        // Act
        buffer.Add("First");
        buffer.Add("Second");
        buffer.Add("Third");

        // Assert
        var items = buffer.GetItems().ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal("First", items[0]);
        Assert.Equal("Second", items[1]);
        Assert.Equal("Third", items[2]);
    }

    [Fact]
    public void Add_ExceedsCapacity_OverwritesOldestItems()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);

        // Act
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4); // Should overwrite 1
        buffer.Add(5); // Should overwrite 2

        // Assert
        var items = buffer.GetItems().ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal(3, items[0]);
        Assert.Equal(4, items[1]);
        Assert.Equal(5, items[2]);
    }

    [Fact]
    public void GetItems_OnEmptyBuffer_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(5);

        // Act
        var items = buffer.GetItems().ToList();

        // Assert
        Assert.Empty(items);
    }

    #endregion

    #region Circular Behavior Tests

    [Fact]
    public void CircularBehavior_FullRotation()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);

        // Act - Add twice the capacity
        for (int i = 1; i <= 6; i++)
        {
            buffer.Add(i);
        }

        // Assert - Should have last 3 items
        var items = buffer.GetItems().ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal(4, items[0]);
        Assert.Equal(5, items[1]);
        Assert.Equal(6, items[2]);
    }

    [Fact]
    public void CircularBehavior_MultipleRotations()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(2);

        // Act
        buffer.Add("A");
        buffer.Add("B");
        buffer.Add("C"); // Overwrites A
        buffer.Add("D"); // Overwrites B
        buffer.Add("E"); // Overwrites C
        buffer.Add("F"); // Overwrites D

        // Assert
        var items = buffer.GetItems().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("E", items[0]);
        Assert.Equal("F", items[1]);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentAdd_MaintainsThreadSafety()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(100);
        var tasks = new List<Task>();

        // Act - Add items from multiple threads
        for (int thread = 0; thread < 10; thread++)
        {
            int threadId = thread;
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    buffer.Add(threadId * 10 + i);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should have exactly 100 items (or less if capacity)
        var items = buffer.GetItems().ToList();
        Assert.Equal(100, items.Count);
        Assert.All(items, item => Assert.InRange(item, 0, 99));
    }

    [Fact]
    public async Task ConcurrentAddAndGet_NoExceptions()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(50);
        var cts = new CancellationTokenSource();
        var exceptions = new List<Exception>();

        // Act - Simultaneously add and read
        var writeTask = Task.Run(async () =>
        {
            try
            {
                int counter = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    buffer.Add($"Item {counter++}");
                    await Task.Delay(1);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        var readTask = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    _ = buffer.GetItems().ToList();
                    await Task.Delay(1);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Let it run for a short time
        await Task.Delay(100);
        cts.Cancel();

        await Task.WhenAll(writeTask, readTask);

        // Assert - No exceptions should occur
        Assert.Empty(exceptions);
    }

    [Fact]
    public void GetItems_DuringConcurrentAdds_ReturnsConsistentSnapshot()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(1000);
        var barrier = new Barrier(2);

        // Act
        var addTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < 1000; i++)
            {
                buffer.Add(i);
            }
        });

        var getTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            Thread.Sleep(10); // Let some adds happen
            return buffer.GetItems().ToList();
        });

        Task.WaitAll(addTask, getTask);
        var snapshot = getTask.Result;

        // Assert - Snapshot should be consistent (all items in sequence)
        for (int i = 1; i < snapshot.Count; i++)
        {
            if (snapshot[i] != 0) // Skip default values
            {
                Assert.True(snapshot[i] >= snapshot[i - 1], 
                    $"Items not in order: {snapshot[i - 1]} followed by {snapshot[i]}");
            }
        }
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void Buffer_WithCapacityOne_WorksCorrectly()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(1);

        // Act
        buffer.Add("First");
        buffer.Add("Second");
        buffer.Add("Third");

        // Assert
        var items = buffer.GetItems().ToList();
        Assert.Single(items);
        Assert.Equal("Third", items[0]);
    }

    [Fact]
    public void Buffer_WithLargeCapacity_WorksCorrectly()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10000);

        // Act
        for (int i = 0; i < 5000; i++)
        {
            buffer.Add(i);
        }

        // Assert
        var items = buffer.GetItems().ToList();
        Assert.Equal(5000, items.Count);
        Assert.Equal(0, items.First());
        Assert.Equal(4999, items.Last());
    }

    [Fact]
    public void Add_NullValues_HandlesCorrectly()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(3);

        // Act
        buffer.Add("First");
        buffer.Add(null);
        buffer.Add("Third");

        // Assert
        var items = buffer.GetItems().ToList();
        Assert.Equal(2, items.Count); // Null values are filtered out in GetItems
        Assert.Equal("First", items[0]);
        Assert.Equal("Third", items[1]);
    }

    [Fact]
    public void GetItems_MultipleCalls_ReturnsSameResults()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);
        for (int i = 1; i <= 3; i++)
        {
            buffer.Add(i);
        }

        // Act
        var items1 = buffer.GetItems().ToList();
        var items2 = buffer.GetItems().ToList();
        var items3 = buffer.GetItems().ToList();

        // Assert
        Assert.Equal(items1, items2);
        Assert.Equal(items2, items3);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void Add_LargeNumberOfItems_PerformsEfficiently()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(1000);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 100000; i++)
        {
            buffer.Add(i);
        }
        sw.Stop();

        // Assert - Should complete quickly (under 1 second)
        Assert.True(sw.ElapsedMilliseconds < 1000, 
            $"Adding 100k items took {sw.ElapsedMilliseconds}ms");
        
        var items = buffer.GetItems().ToList();
        Assert.Equal(1000, items.Count);
    }

    [Fact]
    public void GetItems_OnLargeBuffer_PerformsEfficiently()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(10000);
        for (int i = 0; i < 10000; i++)
        {
            buffer.Add($"Item {i}");
        }

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var items = buffer.GetItems().ToList();
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 100, 
            $"Getting 10k items took {sw.ElapsedMilliseconds}ms");
        Assert.Equal(10000, items.Count);
    }

    #endregion
}