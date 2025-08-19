using System.Diagnostics;
using Scanner111.CLI.Services;

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
        items.Should().ContainSingle("because only one item was added");
        items[0].Should().Be(1);
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
        items.Should().HaveCount(3, "because three items were added");
        items[0].Should().Be("First");
        items[1].Should().Be("Second");
        items[2].Should().Be("Third");
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
        items.Should().HaveCount(3, "because buffer capacity is 3");
        items[0].Should().Be(3, "because oldest items were overwritten");
        items[1].Should().Be(4);
        items[2].Should().Be(5);
    }

    [Fact]
    public void GetItems_OnEmptyBuffer_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(5);

        // Act
        var items = buffer.GetItems().ToList();

        // Assert
        items.Should().BeEmpty("because no items were added");
    }

    #endregion

    #region Circular Behavior Tests

    [Fact]
    public void CircularBehavior_FullRotation()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);

        // Act - Add twice the capacity
        for (var i = 1; i <= 6; i++) buffer.Add(i);

        // Assert - Should have last 3 items
        var items = buffer.GetItems().ToList();
        items.Should().HaveCount(3, "because buffer capacity is 3");
        items[0].Should().Be(4, "because oldest items were overwritten");
        items[1].Should().Be(5);
        items[2].Should().Be(6);
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
        items.Should().HaveCount(2, "because buffer capacity is 2");
        items[0].Should().Be("E", "because earlier items were overwritten");
        items[1].Should().Be("F");
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
        for (var thread = 0; thread < 10; thread++)
        {
            var threadId = thread;
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < 10; i++) buffer.Add(threadId * 10 + i);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should have exactly 100 items (or less if capacity)
        var items = buffer.GetItems().ToList();
        items.Should().HaveCount(100, "because buffer capacity matches items added");
        items.Should().AllSatisfy(item =>
            item.Should().BeInRange(0, 99, "because all items should be within the expected range"));
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
                var counter = 0;
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
        exceptions.Should().BeEmpty("because concurrent operations should be thread-safe");
    }

    [Fact]
    public async Task GetItems_DuringConcurrentAdds_ReturnsConsistentSnapshot()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(1000);
        var barrier = new Barrier(2);

        // Act
        var addTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < 1000; i++) buffer.Add(i);
        });

        var getTask = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await Task.Delay(10); // Let some adds happen
            return buffer.GetItems().ToList();
        });

        await Task.WhenAll(addTask, getTask);
        var snapshot = await getTask;

        // Assert - Snapshot should be consistent (all items in sequence)
        for (var i = 1; i < snapshot.Count; i++)
            if (snapshot[i] != 0) // Skip default values
                snapshot[i].Should().BeGreaterThanOrEqualTo(snapshot[i - 1],
                    $"because items should maintain order: {snapshot[i - 1]} followed by {snapshot[i]}");
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
        items.Should().ContainSingle("because buffer capacity is 1");
        items[0].Should().Be("Third", "because it's the last item added");
    }

    [Fact]
    public void Buffer_WithLargeCapacity_WorksCorrectly()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10000);

        // Act
        for (var i = 0; i < 5000; i++) buffer.Add(i);

        // Assert
        var items = buffer.GetItems().ToList();
        items.Should().HaveCount(5000, "because 5000 items were added within capacity");
        items.First().Should().Be(0, "because items weren't overwritten");
        items.Last().Should().Be(4999);
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
        items.Should().HaveCount(2, "because null values are filtered out in GetItems");
        items[0].Should().Be("First");
        items[1].Should().Be("Third");
    }

    [Fact]
    public void GetItems_MultipleCalls_ReturnsSameResults()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);
        for (var i = 1; i <= 3; i++) buffer.Add(i);

        // Act
        var items1 = buffer.GetItems().ToList();
        var items2 = buffer.GetItems().ToList();
        var items3 = buffer.GetItems().ToList();

        // Assert
        items1.Should().Equal(items2, "because multiple calls should return the same results");
        items2.Should().Equal(items3);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void Add_LargeNumberOfItems_PerformsEfficiently()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(1000);
        var sw = Stopwatch.StartNew();

        // Act
        for (var i = 0; i < 100000; i++) buffer.Add(i);
        sw.Stop();

        // Assert - Should complete quickly (under 1 second)
        sw.ElapsedMilliseconds.Should().BeLessThan(1000,
            $"because adding 100k items should be efficient (took {sw.ElapsedMilliseconds}ms)");

        var items = buffer.GetItems().ToList();
        items.Should().HaveCount(1000, "because buffer capacity is 1000");
    }

    [Fact]
    public void GetItems_OnLargeBuffer_PerformsEfficiently()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(10000);
        for (var i = 0; i < 10000; i++) buffer.Add($"Item {i}");

        // Act
        var sw = Stopwatch.StartNew();
        var items = buffer.GetItems().ToList();
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(100,
            $"because getting 10k items should be efficient (took {sw.ElapsedMilliseconds}ms)");
        items.Should().HaveCount(10000, "because buffer is at full capacity");
    }

    #endregion
}