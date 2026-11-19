using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using ShagShoes.Logic;
using ShagShoes.Models;

namespace ShagShoes.Data
{
    /// <summary>Чтение каталога моделей обуви.</summary>
    public sealed class ProductRepository
    {
        // Количество пар — сумма по всем размерам модели;
        // заказы считаются по датам предыдущего календарного месяца (параметры @PrevStart, @PrevEnd).
        private const string CatalogSql = @"
SELECT p.ProductId, p.Article, p.Name, c.Name AS Category, m.Name AS Manufacturer, m.Country,
       p.Composition, p.Description, p.Price, p.ImageFile,
       ISNULL(q.TotalPairs, 0) AS TotalPairs,
       ISNULL(o.OrdersCount, 0) AS OrdersInPreviousMonth
FROM dbo.Products p
JOIN dbo.Categories c    ON c.CategoryId = p.CategoryId
JOIN dbo.Manufacturers m ON m.ManufacturerId = p.ManufacturerId
OUTER APPLY (
    SELECT SUM(ps.Quantity) AS TotalPairs
    FROM dbo.ProductSizes ps
    WHERE ps.ProductId = p.ProductId
) q
OUTER APPLY (
    SELECT COUNT(DISTINCT od.OrderId) AS OrdersCount
    FROM dbo.OrderItems oi
    JOIN dbo.ProductSizes ps ON ps.ProductSizeId = oi.ProductSizeId
    JOIN dbo.Orders od       ON od.OrderId = oi.OrderId
    WHERE ps.ProductId = p.ProductId
      AND od.OrderDate >= @PrevStart AND od.OrderDate < @PrevEnd
) o
ORDER BY c.Name, p.Name;";

        /// <summary>Каталог с ценами, рассчитанными на указанную дату.</summary>
        public List<Product> GetCatalog(DateTime calcDate)
        {
            DateTime prevStart, prevEnd;
            PriceCalculator.GetPreviousMonth(calcDate, out prevStart, out prevEnd);

            var products = new List<Product>();
            try
            {
                using (SqlConnection connection = Database.Open())
                using (var command = new SqlCommand(CatalogSql, connection))
                {
                    command.Parameters.Add("@PrevStart", SqlDbType.Date).Value = prevStart;
                    command.Parameters.Add("@PrevEnd", SqlDbType.Date).Value = prevEnd;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            products.Add(ReadProduct(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new DatabaseException(
                    "Не удалось прочитать каталог из базы данных.\n\n" +
                    "Проверьте, что база создана последней версией скрипта db\\ShagShoes.sql.\n\n" +
                    "Сообщение сервера: " + ex.Message, ex);
            }
            return products;
        }

        private static Product ReadProduct(SqlDataReader reader)
        {
            var product = new Product
            {
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                Article = reader.GetString(reader.GetOrdinal("Article")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Category = reader.GetString(reader.GetOrdinal("Category")),
                Manufacturer = reader.GetString(reader.GetOrdinal("Manufacturer")),
                Country = reader.GetString(reader.GetOrdinal("Country")),
                Composition = reader.GetString(reader.GetOrdinal("Composition")),
                Description = ReadNullableString(reader, "Description"),
                ImageFile = ReadNullableString(reader, "ImageFile"),
                Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                TotalPairs = reader.GetInt32(reader.GetOrdinal("TotalPairs")),
                OrdersInPreviousMonth = reader.GetInt32(reader.GetOrdinal("OrdersInPreviousMonth"))
            };
            product.FinalPrice = PriceCalculator.GetPrice(product.Price, product.OrdersInPreviousMonth);
            return product;
        }

        private static string ReadNullableString(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
    }
}
