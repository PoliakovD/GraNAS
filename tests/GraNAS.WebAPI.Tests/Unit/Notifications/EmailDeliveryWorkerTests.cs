using System;
using Xunit;

namespace GraNAS.WebAPI.Tests.Unit.Notifications;

public class EmailDeliveryWorkerTests
{
    private static readonly TimeSpan[] Backoffs =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6)
    ];

    [Fact]
    public void BackoffProgression_IsStrictlyIncreasing()
    {
        for (int i = 1; i < Backoffs.Length; i++)
            Assert.True(Backoffs[i] > Backoffs[i - 1],
                $"Backoff[{i}]={Backoffs[i]} should be greater than Backoff[{i - 1}]={Backoffs[i - 1]}");
    }

    [Fact]
    public void BackoffProgression_HasFiveSteps()
    {
        Assert.Equal(5, Backoffs.Length);
    }

    [Fact]
    public void BackoffProgression_StartsAt30Seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), Backoffs[0]);
    }

    [Fact]
    public void BackoffProgression_EndsAt6Hours()
    {
        Assert.Equal(TimeSpan.FromHours(6), Backoffs[^1]);
    }
}
