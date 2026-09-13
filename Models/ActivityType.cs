namespace EcoQuest.Models
{
    public enum InputControlType
    {
        Numeric,
        TimeRange,
        Count,
        Toggle
    }

    public class ActivityType
    {
        private int _activityTypeId;
        private string _typeName;
        private int _categoryId;
        private InputControlType _inputControlType;
        private decimal _co2Coefficient;
        private decimal _pointFactor;

        public int ActivityTypeId
        {
            get => _activityTypeId;
            private set => _activityTypeId = value;
        }

        public string TypeName
        {
            get => _typeName;
            private set => _typeName = value;
        }

        public int CategoryId
        {
            get => _categoryId;
            private set => _categoryId = value;
        }

        public InputControlType InputControlType
        {
            get => _inputControlType;
            private set => _inputControlType = value;
        }

        public decimal Co2Coefficient
        {
            get => _co2Coefficient;
            private set => _co2Coefficient = value;
        }

        public decimal PointFactor
        {
            get => _pointFactor;
            private set => _pointFactor = value;
        }

        public ActivityType(
            int activityTypeId,
            string typeName,
            int categoryId,
            InputControlType inputControlType,
            decimal co2Coefficient,
            decimal pointFactor)
        {
            ActivityTypeId = activityTypeId;
            TypeName = typeName;
            CategoryId = categoryId;
            InputControlType = inputControlType;
            Co2Coefficient = co2Coefficient;
            PointFactor = pointFactor;
        }

        public bool BelongsToCategory(ActivityCategory category)
        {
            return CategoryId == category.CategoryId;
        }
        public bool IsQuantityPlausible(decimal qty)
        {
            if (qty < 0)
                return false;

            switch (InputControlType)
            {
                case InputControlType.Numeric:
                    return qty > 0;

                case InputControlType.TimeRange:
                    return qty > 0 && qty <= 24;

                case InputControlType.Count:
                    return qty >= 1 && decimal.Truncate(qty) == qty;

                case InputControlType.Toggle:
                    return qty == 0 || qty == 1;

                default:
                    return false;
            }
        }
        
    }
}