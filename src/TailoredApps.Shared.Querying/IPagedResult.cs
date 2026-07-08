using System.Collections.Generic;

namespace TailoredApps.Shared.Querying
{
    /// <summary>Reprezentuje stronicowany wynik zapytania.</summary>
    /// <typeparam name="T">Typ elementu kolekcji wyników.</typeparam>
    public interface IPagedResult<T>
    {
        /// <summary>Kolekcja wyników na bieżącej stronie.</summary>
        ICollection<T> Results { get; set; }

        /// <summary>Łączna liczba wszystkich wyników (bez stronicowania).</summary>
        int Count { get; set; }
    }
}
