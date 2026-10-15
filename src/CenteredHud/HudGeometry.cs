using System;

namespace CenteredHud
{
    // Pure geometry, independent of Unity and of the game's UI scale algorithm.
    public static class HudGeometry
    {
        public static double Width(double screenWidth, double requestedWidth, double canvasScale)
        {
            if (screenWidth <= 0 || double.IsNaN(screenWidth) || double.IsInfinity(screenWidth))
                return 0;
            if (requestedWidth <= 0 || double.IsNaN(requestedWidth) || double.IsInfinity(requestedWidth))
                return screenWidth;
            if (canvasScale <= 0 || double.IsNaN(canvasScale) || double.IsInfinity(canvasScale))
                canvasScale = 1;
            // Below this width ONI's management and build panels become impractical.
            return Math.Min(screenWidth, Math.Max(Math.Min(screenWidth, 1280 * canvasScale), requestedWidth));
        }

        public static float MapAnchor(float anchor, double widthFraction)
        {
            return (float)((1 - widthFraction) * 0.5 + anchor * widthFraction);
        }

        public static int StepWidth(int screenWidth, int requestedWidth, double canvasScale, int delta)
        {
            if (screenWidth <= 0) return 0;
            if (canvasScale <= 0 || double.IsNaN(canvasScale) || double.IsInfinity(canvasScale)) canvasScale = 1;
            double current = Width(screenWidth, requestedWidth, canvasScale);
            double minimum = Math.Min(screenWidth, 1280 * canvasScale);
            return (int)Math.Min(screenWidth, Math.Max(minimum, current + delta));
        }
    }
}
