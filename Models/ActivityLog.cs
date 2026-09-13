using System;

namespace EcoQuest.Models
{
    public class ActivityLog
    {
        private Guid _logId;
        private Guid _userId;
        private int _activityTypeId;
        private DateTime _logDate;
        private decimal _quantity;
        private TimeSpan _startTime;
        private TimeSpan? _endTime;
        private int _pointsEarned;
        private decimal _co2SavedKg;
        private DateTime _createdAt;

        public Guid LogId
        {
            get => _logId;
            set => _logId = value;
        }

        public Guid UserId
        {
            get => _userId;
            set => _userId = value;
        }

        public int ActivityTypeId
        {
            get => _activityTypeId;
            set => _activityTypeId = value;
        }

        public DateTime LogDate
        {
            get => _logDate;
            set => _logDate = value;
        }

        public decimal Quantity
        {
            get => _quantity;
            set => _quantity = value;
        }

        public TimeSpan StartTime
        {
            get => _startTime;
            set => _startTime = value;
        }

        public TimeSpan? EndTime
        {
            get => _endTime;
            set => _endTime = value;
        }

        public int PointsEarned
        {
            get => _pointsEarned;
            private set => _pointsEarned = value;
        }

        public decimal Co2SavedKg
        {
            get => _co2SavedKg;
            set => _co2SavedKg = value;
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => _createdAt = value;
        }

        public ActivityLog(
            Guid userId,
            int activityTypeId,
            DateTime logDate,
            decimal quantity,
            TimeSpan startTime,
            TimeSpan? endTime,
            int pointsEarned,
            decimal co2SavedKg)
        {
            LogId = Guid.NewGuid();
            UserId = userId;
            ActivityTypeId = activityTypeId;
            LogDate = logDate;
            Quantity = quantity;
            StartTime = startTime;
            EndTime = endTime;
            PointsEarned = pointsEarned;
            Co2SavedKg = co2SavedKg;
            CreatedAt = DateTime.Now;
        }

        public double GetDurationInHours()
        {
            if (!EndTime.HasValue)
                return 0;

            TimeSpan duration = EndTime.Value - StartTime;

            if (duration.TotalHours < 0)
                return 0;

            return duration.TotalHours;
        }
    }
}