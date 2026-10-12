using System.Windows;
using ReWASD.UI.TesterControls;
using ReWASD.UI.Views;

namespace ReWASD.UI.Tests;

/// <summary>The Windows layout of the live readings under the controller.</summary>
public class LiveDeckTests
{
    [Fact]
    public void TheDrawingKeepsItsSize_AndTheRestGoesToTheReadings()
    {
        Assert.Equal((500.0, 0.0), StagePanel.Split(500, true, 480, 200, 620));
        Assert.Equal((480.0, 300.0), StagePanel.Split(780, true, 480, 200, 620));
        Assert.Equal((680.0, 620.0), StagePanel.Split(1300, true, 480, 200, 620));
        Assert.Equal((900.0, 0.0), StagePanel.Split(900, false, 480, 200, 620));
    }

    [Fact]
    public void ThePressureBar_FillsFromTheBottom()
    {
        var track = new Rect(0, 0, 10, 100);
        Assert.Equal(new Rect(0, 75, 10, 25), PressureBar.FillRect(track, 0.25));
        Assert.Equal(new Rect(0, 0, 10, 100), PressureBar.FillRect(track, 1.4));
        Assert.Equal(0, PressureBar.FillRect(track, -1).Height);
    }

    [Fact]
    public void ThePanel_LeavesTheReadingsOut_WhenThereIsNoRoom_AndShowsThemWhenThereIs() => Sta.Run(() =>
    {
        var stage = new StagePanel();
        var drawing = new System.Windows.Controls.Border();
        var deck = new System.Windows.Controls.Border { Visibility = Visibility.Collapsed };
        stage.Children.Add(drawing);
        stage.Children.Add(deck);

        stage.Measure(new Size(900, 560));
        stage.Arrange(new Rect(0, 0, 900, 560));
        Assert.False(stage.IsDeckShown);
        Assert.Equal(Visibility.Collapsed, deck.Visibility);
        Assert.Equal(560, drawing.RenderSize.Height);

        stage.Measure(new Size(900, 860));
        stage.Arrange(new Rect(0, 0, 900, 860));
        Assert.True(stage.IsDeckShown);
        Assert.Equal(Visibility.Visible, deck.Visibility);
        Assert.Equal(480, drawing.RenderSize.Height);
    });
}
