using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Application.Time;
using Vulperonex.Application.Twitch;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Domain.Members;
using Vulperonex.Web.SignalR;
using Xunit;

namespace Vulperonex.Tests.Unit.Overlay;

public sealed class OverlayEventForwarderTests
{
    private readonly IStreamEventBus _mockEventBus = Substitute.For<IStreamEventBus>();
    private readonly Subject<IStreamEvent> _eventStream = new();

    private readonly IHubContext<EventsHub> _mockEventsHub = Substitute.For<IHubContext<EventsHub>>();
    private readonly IHubContext<OverlayChatHub> _mockChatHub = Substitute.For<IHubContext<OverlayChatHub>>();
    private readonly IHubContext<OverlayAlertsHub> _mockAlertsHub = Substitute.For<IHubContext<OverlayAlertsHub>>();
    private readonly IHubContext<OverlayMemberHub> _mockMemberHub = Substitute.For<IHubContext<OverlayMemberHub>>();

    private readonly IOverlayHistoryService<OverlayChatPayload> _mockChatHistory = Substitute.For<IOverlayHistoryService<OverlayChatPayload>>();
    private readonly IOverlayHistoryService<OverlayAlertPayload> _mockAlertsHistory = Substitute.For<IOverlayHistoryService<OverlayAlertPayload>>();
    private readonly IOverlayHistoryService<OverlayMemberPayload> _mockMemberHistory = Substitute.For<IOverlayHistoryService<OverlayMemberPayload>>();

    private readonly IServiceScopeFactory _mockScopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly ITwitchBadgeCache _mockBadgeCache = Substitute.For<ITwitchBadgeCache>();
    private readonly IClock _mockClock = Substitute.For<IClock>();

    public OverlayEventForwarderTests()
    {
        _mockEventBus.Events.Returns(_eventStream);
        _mockClock.UtcNow.Returns(new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero));

        // Setup Hub Clients mocking
        SetupHubClients(_mockEventsHub);
        SetupHubClients(_mockChatHub);
        SetupHubClients(_mockAlertsHub);
        SetupHubClients(_mockMemberHub);
    }

    private static void SetupHubClients<THub>(IHubContext<THub> mockHub) where THub : Hub
    {
        var mockClients = Substitute.For<IHubClients>();
        var mockClientProxy = Substitute.For<IClientProxy>();
        mockClients.All.Returns(mockClientProxy);
        mockHub.Clients.Returns(mockClients);
    }

    [Fact]
    public async Task Given_MemberCheckedInEvent_When_Received_Then_ForwardsToOverlayMemberHubAndHistory()
    {
        // Arrange
        var forwarder = new OverlayEventForwarder(
            _mockEventBus,
            _mockEventsHub,
            _mockChatHub,
            _mockAlertsHub,
            _mockMemberHub,
            _mockChatHistory,
            _mockAlertsHistory,
            _mockMemberHistory,
            _mockScopeFactory,
            _mockBadgeCache,
            _mockClock,
            NullLogger<OverlayEventForwarder>.Instance);

        await forwarder.StartAsync(CancellationToken.None);

        var streamUser = new StreamUser("twitch", "alice", "Alice", StreamRole.None);
        var checkInEvent = new MemberCheckedInEvent
        {
            Platform = "twitch",
            User = streamUser,
            AvatarUrl = "http://avatar",
            CheckInCount = 5,
            TotalLoyalty = 100,
            RoundIndex = 1,
            StampSlotInRound = 5
        };

        // Act
        _eventStream.OnNext(checkInEvent);

        // Assert
        // Verify history persistence
        await _mockMemberHistory.Received(1).AddAsync(
            Arg.Is<OverlayMemberPayload>(p =>
                p.DisplayName == "Alice" &&
                p.AvatarUrl == "http://avatar" &&
                p.CheckInCount == 5 &&
                p.RoundIndex == 1 &&
                p.StampSlotInRound == 5),
            Arg.Any<CancellationToken>());

        // Clean up
        await forwarder.StopAsync(CancellationToken.None);
    }
}
