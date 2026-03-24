using System;

namespace TailoredApps.Shared.Querying
{
    /// <summary>Bazowa klasa zapytania stronicowanego i sortowanego.</summary>
    /// <typeparam name="TQuery">Typ filtru zapytania.</typeparam>
    public abstract class PagedAndSortedQuery<TQuery> : IPagedAndSortedQuery<TQuery> where TQuery : QueryBase
    {
        /// <summary>Numer strony (1-based).</summary>
        public int? Page { get; set; }

        /// <summary>Liczba elementów na stronie.</summary>
        public int? Count { get; set; }

        /// <summary>Czy parametry stronicowania są podane.</summary>
        public bool IsPagingSpecified => Page.HasValue && Count.HasValue;

        /// <summary>Pole sortowania.</summary>
        public string SortField { get; set; }

        /// <summary>Kierunek sortowania.</summary>
        public SortDirection? SortDir { get; set; }

        /// <summary>Czy parametry sortowania są podane.</summary>
        public bool IsSortingSpecified => !string.IsNullOrWhiteSpace(SortField) && SortDir.HasValue;

        /// <summary>Obiekt filtra zapytania.</summary>
        public TQuery Filter { get; set; }

        /// <summary>Sprawdza, czy zapytanie jest sortowane po wskazanym polu.</summary>
        /// <param name="fieldName">Nazwa pola do sprawdzenia.</param>
        public bool IsSortBy(string fieldName) => string.Equals(SortField, fieldName, StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>Interfejs zapytania stronicowanego i sortowanego.</summary>
    /// <typeparam name="TQuery">Typ filtru zapytania.</typeparam>
    public interface IPagedAndSortedQuery<TQuery> : IQuery<TQuery>, IQueryParameters where TQuery : QueryBase
    {
        /// <summary>Numer strony (1-based).</summary>
        new int? Page { get; set; }

        /// <summary>Liczba elementów na stronie.</summary>
        new int? Count { get; set; }

        /// <summary>Czy parametry stronicowania są podane.</summary>
        new bool IsPagingSpecified { get; }

        /// <summary>Pole sortowania.</summary>
        new string SortField { get; set; }

        /// <summary>Kierunek sortowania.</summary>
        new SortDirection? SortDir { get; set; }

        /// <summary>Czy parametry sortowania są podane.</summary>
        new bool IsSortingSpecified { get; }

        /// <summary>Obiekt filtra zapytania.</summary>
        new TQuery Filter { get; set; }

        /// <summary>Sprawdza, czy zapytanie jest sortowane po wskazanym polu.</summary>
        bool IsSortBy(string fieldName);
    }

    /// <summary>Parametry stronicowania.</summary>
    public interface IPagingParameters
    {
        /// <summary>Numer strony.</summary>
        int? Page { get; }

        /// <summary>Liczba elementów na stronie.</summary>
        int? Count { get; }

        /// <summary>Czy parametry stronicowania są podane.</summary>
        bool IsPagingSpecified { get; }
    }

    /// <summary>Parametry sortowania.</summary>
    public interface ISortingParameters
    {
        /// <summary>Pole sortowania.</summary>
        string SortField { get; }

        /// <summary>Kierunek sortowania.</summary>
        SortDirection? SortDir { get; }

        /// <summary>Czy parametry sortowania są podane.</summary>
        bool IsSortingSpecified { get; }
    }

    /// <summary>Połączone parametry stronicowania i sortowania.</summary>
    public interface IQueryParameters : IPagingParameters, ISortingParameters
    {
    }

    /// <summary>Interfejs zapytania z filtrem.</summary>
    /// <typeparam name="T">Typ filtru.</typeparam>
    public interface IQuery<T>
    {
        /// <summary>Obiekt filtra zapytania.</summary>
        T Filter { get; set; }
    }

    /// <summary>Interfejs stronicowanego żądania MediatR z filtrem i modelem odpowiedzi.</summary>
    /// <typeparam name="TResponse">Typ odpowiedzi (musi implementować <see cref="IPagedResult{TModel}"/>).</typeparam>
    /// <typeparam name="TQuery">Typ filtru zapytania.</typeparam>
    /// <typeparam name="TModel">Typ elementu w wynikach.</typeparam>
    public interface IPagedAndSortedRequest<TResponse, TQuery, TModel> : IPagedAndSortedQuery<TQuery>
        where TQuery : QueryBase
        where TResponse : IPagedResult<TModel>
    {
    }
}
