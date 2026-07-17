namespace FreezerLegoMeals.Domain.DotNet;

public class IngredientAmount
{
    public double Amount { get; set; }
    public string Unit { get; set; }
    
    public IngredientAmount(double amount, string unit)
    {
        Amount = amount;
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
    }
    
    public override bool Equals(object obj)
    {
        if (obj is IngredientAmount other)
        {
            return Amount == other.Amount && Unit == other.Unit;
        }
        return false;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Amount, Unit);
    }
}