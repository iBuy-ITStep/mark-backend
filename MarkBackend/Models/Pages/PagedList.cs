using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace MarkBackend.Models.Pages
{
    /// <summary>
    /// A wrapper class for paginated data, ensuring proper JSON serialization for APIs.
    /// </summary>
    /// <typeparam name="T">The type of the data being paginated.</typeparam>
    public class PagedList<T>
    {
        /// <summary>
        /// The collection of items for the current page.
        /// </summary>
        public IEnumerable<T> Items { get; set; }

        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        public QueryOptions Options { get; set; }

        private PagedList(IEnumerable<T> items, int count, QueryOptions options)
        {
            Items = items;
            CurrentPage = options.CurrentPage;
            PageSize = options.PageSize;
            Options = options;

            TotalPages = (int)Math.Ceiling(count / (double)PageSize);
        }

        /// <summary>
        /// Asynchronously creates a paginated list after applying search, sort, and pagination logic to the query.
        /// </summary>
        public static async Task<PagedList<T>> CreateAsync(IQueryable<T> query, QueryOptions options)
        {
            if (options != null)
            {
                if (!string.IsNullOrEmpty(options.OrderPropertyName))
                {
                    query = Order(query, options.OrderPropertyName, options.DescendingOrder);
                }
                if (!string.IsNullOrEmpty(options.SearchPropertyName) && !string.IsNullOrEmpty(options.SearchTerm))
                {
                    query = Search(query, options.SearchPropertyName, options.SearchTerm);
                }
            }

            var count = await query.CountAsync();
            var items = await query.Skip((options!.CurrentPage - 1) * options.PageSize).Take(options.PageSize).ToListAsync();

            return new PagedList<T>(items, count, options);
        }

        private static IQueryable<T> Search(IQueryable<T> query, string propertyName, string searchTerm)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var source = propertyName.Split('.').Aggregate((Expression)parameter, Expression.Property);
            var body = Expression.Call(source, "Contains", Type.EmptyTypes, Expression.Constant(searchTerm, typeof(string)));
            var lambda = Expression.Lambda<Func<T, bool>>(body, parameter);
            return query.Where(lambda);
        }

        private static IQueryable<T> Order(IQueryable<T> query, string propertyName, bool desc)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var source = propertyName.Split('.').Aggregate((Expression)parameter, Expression.Property);
            var lambda = Expression.Lambda(typeof(Func<,>).MakeGenericType(typeof(T), source.Type), source, parameter);

            var method = typeof(Queryable).GetMethods().Single(e =>
                e.Name == (desc ? "OrderByDescending" : "OrderBy") &&
                e.IsGenericMethodDefinition &&
                e.GetGenericArguments().Length == 2 &&
                e.GetParameters().Length == 2);

            var genericMethod = method.MakeGenericMethod(typeof(T), source.Type);

            return (IQueryable<T>)genericMethod.Invoke(null, new object[] { query, lambda })!;
        }
    }
}