﻿namespace SmartRecipes.Mobile.Models
{
    public interface IAmount
    {
        int Count { get; }

        AmountUnit Unit { get; }

        IAmount WithCount(int count);
    }
}
