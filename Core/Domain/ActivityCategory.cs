using System;

namespace EcoQuest.Core.Domain;

public sealed class ActivityCategory
{
    public int CategoryId { get; }
    public string CategoryName { get; }
    public string? Description { get; }

    public ActivityCategory(int categoryId, string categoryName, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            throw new ArgumentException("Nama kategori tidak boleh kosong.", nameof(categoryName));

        CategoryId = categoryId;
        CategoryName = categoryName;
        Description = description;
    }
}
