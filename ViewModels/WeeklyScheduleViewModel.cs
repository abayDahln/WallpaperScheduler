using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;

namespace WallpaperScheduler.ViewModels
{
    public partial class WeeklyScheduleViewModel : ObservableObject
    {
        private readonly ConfigService _configService;

        public WeeklyScheduleViewModel(ConfigService configService)
        {
            _configService = configService;
        }

        public List<TimeSlot> GetDaySlots(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => _configService.Config.WeeklySchedule.Monday,
                DayOfWeek.Tuesday => _configService.Config.WeeklySchedule.Tuesday,
                DayOfWeek.Wednesday => _configService.Config.WeeklySchedule.Wednesday,
                DayOfWeek.Thursday => _configService.Config.WeeklySchedule.Thursday,
                DayOfWeek.Friday => _configService.Config.WeeklySchedule.Friday,
                DayOfWeek.Saturday => _configService.Config.WeeklySchedule.Saturday,
                DayOfWeek.Sunday => _configService.Config.WeeklySchedule.Sunday,
                _ => new List<TimeSlot>()
            };
        }

        private void SetDaySlots(DayOfWeek day, List<TimeSlot> slots)
        {
            switch (day)
            {
                case DayOfWeek.Monday: _configService.Config.WeeklySchedule.Monday = slots; break;
                case DayOfWeek.Tuesday: _configService.Config.WeeklySchedule.Tuesday = slots; break;
                case DayOfWeek.Wednesday: _configService.Config.WeeklySchedule.Wednesday = slots; break;
                case DayOfWeek.Thursday: _configService.Config.WeeklySchedule.Thursday = slots; break;
                case DayOfWeek.Friday: _configService.Config.WeeklySchedule.Friday = slots; break;
                case DayOfWeek.Saturday: _configService.Config.WeeklySchedule.Saturday = slots; break;
                case DayOfWeek.Sunday: _configService.Config.WeeklySchedule.Sunday = slots; break;
            }
            _configService.SaveConfig();
        }

        public void AddSlot(DayOfWeek day, TimeSlot slot)
        {
            var slots = GetDaySlots(day);
            slots.Add(slot);
            SetDaySlots(day, slots);
        }

        public void RemoveSlot(DayOfWeek day, TimeSlot slot)
        {
            var slots = GetDaySlots(day);
            slots.Remove(slot);
            SetDaySlots(day, slots);
        }

        public void CopyDaySchedule(DayOfWeek source, DayOfWeek dest)
        {
            if (source == dest) return;
            var srcSlots = GetDaySlots(source);
            var newSlots = srcSlots.Select(s => new TimeSlot
            {
                Start = s.Start,
                End = s.End,
                WallpaperId = s.WallpaperId
            }).ToList();
            SetDaySlots(dest, newSlots);
        }
    }
}