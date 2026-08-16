using System;
using System.Collections.Generic;
using System.Linq;
using WallpaperScheduler.Models;

namespace WallpaperScheduler.Helpers
{
    public static class ScheduleResolver
    {
        public static (string? WallpaperId, string? Style) ResolveActiveWallpaper(AppConfig config, DateTime now, ref string? lastAppliedWallpaperId)
        {
            string dateStr = now.ToString("yyyy-MM-dd");
            TimeSpan time = now.TimeOfDay;

            // 1. Specific Date Override
            var dateOverride = config.DateOverrides.FirstOrDefault(d => d.Date == dateStr);
            if (dateOverride != null && dateOverride.Slots.Count > 0)
            {
                var r = ResolveFromSlots(dateOverride.Slots, time, lastAppliedWallpaperId, config.Settings.DefaultWallpaperId);
                if (r.WallpaperId != null) lastAppliedWallpaperId = r.WallpaperId;
                return (r.WallpaperId ?? config.Settings.DefaultWallpaperId, r.Slot?.WallpaperStyle);
            }

            // 2. Monthly Recurring Override
            int dayOfMonth = now.Day;
            var monthlyOverride = config.MonthlyOverrides.FirstOrDefault(m => m.DayOfMonth == dayOfMonth);
            if (monthlyOverride != null && monthlyOverride.Slots.Count > 0)
            {
                var r = ResolveFromSlots(monthlyOverride.Slots, time, lastAppliedWallpaperId, config.Settings.DefaultWallpaperId);
                if (r.WallpaperId != null) lastAppliedWallpaperId = r.WallpaperId;
                return (r.WallpaperId ?? config.Settings.DefaultWallpaperId, r.Slot?.WallpaperStyle);
            }

            // 3. Weekly Schedule
            var weeklySlots = config.WeeklySchedule.GetDaySlots(now.DayOfWeek);
            if (weeklySlots.Count > 0)
            {
                var r = ResolveFromSlots(weeklySlots, time, lastAppliedWallpaperId, config.Settings.DefaultWallpaperId);
                if (r.WallpaperId != null) lastAppliedWallpaperId = r.WallpaperId;
                return (r.WallpaperId ?? config.Settings.DefaultWallpaperId, r.Slot?.WallpaperStyle);
            }

            // 4. Default Fallback
            return (config.Settings.DefaultWallpaperId, null);
        }

        private static (TimeSlot? Slot, string? WallpaperId) ResolveFromSlots(List<TimeSlot> slots, TimeSpan time, string? lastApplied, string? defaultWp)
        {
            // hanya slot yang aktif pada waktu ini yang dipakai;
            // kalau tidak ada slot aktif -> null (caller pakai default wallpaper)
            var matchingSlot = slots.FirstOrDefault(s => s.StartTimeSpan <= time && time < s.EndTimeSpan);
            if (matchingSlot != null) return (matchingSlot, matchingSlot.WallpaperId);
            return (null, null);
        }

        public static DateTime GetNextEventTime(AppConfig config, DateTime now)
        {
            DateTime todayMidnight = now.Date;
            DateTime tomorrowMidnight = todayMidnight.AddDays(1);
            List<DateTime> candidates = new() { tomorrowMidnight };

            TimeSpan time = now.TimeOfDay;
            List<TimeSlot>? activeSlots = null;

            var dateOverride = config.DateOverrides.FirstOrDefault(d => d.Date == now.ToString("yyyy-MM-dd"));
            if (dateOverride != null && dateOverride.Slots.Count > 0) activeSlots = dateOverride.Slots;
            else
            {
                var monthlyOverride = config.MonthlyOverrides.FirstOrDefault(m => m.DayOfMonth == now.Day);
                if (monthlyOverride != null && monthlyOverride.Slots.Count > 0) activeSlots = monthlyOverride.Slots;
                else activeSlots = config.WeeklySchedule.GetDaySlots(now.DayOfWeek);
            }

            if (activeSlots != null)
            {
                foreach (var slot in activeSlots)
                {
                    DateTime startDt = todayMidnight.Add(slot.StartTimeSpan);
                    DateTime endDt = slot.End == "24:00" ? tomorrowMidnight : todayMidnight.Add(slot.EndTimeSpan);

                    if (startDt > now) candidates.Add(startDt);
                    if (endDt > now) candidates.Add(endDt);
                }
            }

            return candidates.Min();
        }
    }
}
