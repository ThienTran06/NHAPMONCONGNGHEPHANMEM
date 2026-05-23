using System.Reflection;

namespace DormitoryManagement.WPF.Tests;

public sealed class ShellViewAnimationTests
{
    private const string SettingsTypeName = "DormitoryManagement.WPF.Views.Shared.ShellViewAnimationSettings, DormitoryManagement.WPF";

    [Fact]
    public void Hidden_offset_places_sidebar_fully_offscreen()
    {
        var settingsType = GetSettingsType();

        var sidebarWidth = GetDouble(settingsType, "SidebarWidth");
        var hiddenOffset = GetDouble(settingsType, "HiddenOffset");
        var openOffset = GetDouble(settingsType, "OpenOffset");

        Assert.Equal(312d, sidebarWidth);
        Assert.Equal(-sidebarWidth, hiddenOffset);
        Assert.Equal(0d, openOffset);
    }

    [Fact]
    public void Animation_durations_match_contract_limits()
    {
        var settingsType = GetSettingsType();

        var openDuration = GetTimeSpan(settingsType, "OpenDuration");
        var closeDuration = GetTimeSpan(settingsType, "CloseDuration");

        Assert.True(openDuration > TimeSpan.Zero);
        Assert.True(openDuration <= TimeSpan.FromMilliseconds(350));
        Assert.True(closeDuration > TimeSpan.Zero);
        Assert.True(closeDuration <= TimeSpan.FromMilliseconds(300));
    }

    [Fact]
    public void Hotspot_width_is_narrow_left_edge_trigger()
    {
        var settingsType = GetSettingsType();

        var hotspotWidth = GetDouble(settingsType, "HotspotWidth");

        Assert.InRange(hotspotWidth, 6d, 10d);
    }

    [Fact]
    public void Close_delay_is_short_and_positive_for_reentry_cancellation()
    {
        var settingsType = GetSettingsType();

        var closeDelay = GetTimeSpan(settingsType, "CloseDelay");

        Assert.InRange(closeDelay.TotalMilliseconds, 50d, 200d);
    }

    private static Type GetSettingsType()
    {
        var settingsType = Type.GetType(SettingsTypeName);

        Assert.NotNull(settingsType);
        return settingsType;
    }

    private static double GetDouble(Type settingsType, string memberName) =>
        Convert.ToDouble(GetStaticValue(settingsType, memberName));

    private static TimeSpan GetTimeSpan(Type settingsType, string memberName)
    {
        var value = GetStaticValue(settingsType, memberName);

        Assert.IsType<TimeSpan>(value);
        return (TimeSpan)value;
    }

    private static object? GetStaticValue(Type settingsType, string memberName)
    {
        var property = settingsType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
        if (property is not null)
        {
            return property.GetValue(null);
        }

        var field = settingsType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(field);
        return field.GetValue(null);
    }
}
