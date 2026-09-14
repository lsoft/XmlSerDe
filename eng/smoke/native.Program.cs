using System;
using System.Collections.Generic;
using XmlSerDe;

namespace Smoke
{
    /// <summary>Строка заказа.</summary>
    public class OrderLine
    {
        /// <summary>Товар.</summary>
        public string Product { get; set; } = "";

        /// <summary>Количество.</summary>
        public int Quantity { get; set; }
    }

    /// <summary>Заказ.</summary>
    public class Order
    {
        /// <summary>Номер.</summary>
        public int Id { get; set; }

        /// <summary>Строки.</summary>
        public List<OrderLine> Lines { get; set; } = new List<OrderLine>();
    }

    /// <summary>Сериализатор: ровно то, что пишет потребитель.</summary>
    [XmlSubject(typeof(OrderLine), false)]
    [XmlSubject(typeof(Order), true)]
    public partial class OrderSerializer
    {
    }

    /// <summary>Точка входа.</summary>
    public static class Program
    {
        /// <summary>Точка входа.</summary>
        public static void Main()
        {
            var order = new Order { Id = 7 };
            order.Lines.Add(new OrderLine { Product = "Bolt", Quantity = 3 });

            var exhauster = new StringBuilderExhauster();
            OrderSerializer.Serialize(exhauster, order, false);
            var xml = exhauster.ToString();
            Console.WriteLine("XML: " + xml);

            OrderSerializer.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out Order back);
            Console.WriteLine("BACK: Id=" + back.Id + " Lines=" + back.Lines.Count + " Product=" + back.Lines[0].Product);
        }
    }
}
