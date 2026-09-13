namespace EcoQuest.Models

{
    public class ActivityCategory
    {
        private int _categoryId;
        private string _categoryName;
        private string _categoryDescription;

        public int CategoryId { 
            get => _categoryId;
            private set => _categoryId = value; 
        }
        public string CategoryName { 
            get => _categoryName;
            private set => _categoryName = value; }
        public string CategoryDescription { 
            get => _categoryDescription;
            private set => _categoryDescription = value; }
        public ActivityCategory(
        int categoryId,
        string categoryName,
        string description)
    {
        CategoryId = categoryId;
        CategoryName = categoryName;
        CategoryDescription = description;
    }
    }
}