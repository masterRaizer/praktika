namespace ShagShoes.Models
{
    /// <summary>Модель обуви в каталоге вместе с рассчитанной ценой.</summary>
    public sealed class Product
    {
        public int ProductId { get; set; }
        public string Article { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Manufacturer { get; set; }
        public string Country { get; set; }
        public string Composition { get; set; }
        public string Description { get; set; }
        public string ImageFile { get; set; }

        /// <summary>Цена модели в каталоге, руб.</summary>
        public decimal Price { get; set; }

        /// <summary>Цена с учётом скидки, руб.</summary>
        public decimal FinalPrice { get; set; }

        /// <summary>Доступное количество пар суммарно по всем размерам.</summary>
        public int TotalPairs { get; set; }

        /// <summary>Заказов модели в предыдущем календарном месяце.</summary>
        public int OrdersInPreviousMonth { get; set; }

        public bool HasDiscount
        {
            get { return FinalPrice < Price; }
        }

        /// <summary>Строка «Производство | Наименование» как в макете каталога.</summary>
        public string Title
        {
            get { return Manufacturer + " | " + Name; }
        }
    }
}
