﻿// -----------------------------------------------------------------------
// <copyright file="LineSeriesWithTracker.cs" company="CSIRO">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace Utility
{
    using System;
    using OxyPlot;
    using OxyPlot.Series;
    using UserInterface.EventArguments;

    /// <summary>
    /// A line series with a better tracker.
    /// </summary>
    public class LineSeriesWithTracker : LineSeries
    {
        /// <summary>
        /// Invoked when the user hovers over a series point.
        /// </summary>
        public event EventHandler<HoverPointArgs> OnHoverOverPoint;

        /// <summary>
        /// Tracker is calling to determine the nearest point.
        /// </summary>
        /// <param name="point">The point clicked</param>
        /// <param name="interpolate">A value indicating whether interpolation should be used.</param>
        /// <returns>The return hit result</returns>
        public override TrackerHitResult GetNearestPoint(OxyPlot.ScreenPoint point, bool interpolate)
        {
            TrackerHitResult hitResult = base.GetNearestPoint(point, interpolate);

            if (hitResult != null && OnHoverOverPoint != null)
            {
                HoverPointArgs e = new HoverPointArgs();
                e.SeriesName = Title;
                e.X = hitResult.DataPoint.X;
                e.Y = hitResult.DataPoint.Y;
                OnHoverOverPoint.Invoke(this, e);
                if (e.HoverText != null)
                {
                    hitResult.Series.TrackerFormatString = e.HoverText + "\n{1}: {2}\n{3}: {4}";
                }
            }

            return hitResult;
        }
    }
}
