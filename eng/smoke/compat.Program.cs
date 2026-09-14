using System;
using System.IO;

namespace Smoke
{
    /// <summary>Заказ.</summary>
    public class Order
    {
        /// <summary>Номер.</summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// Потребитель, подключивший ТОЛЬКО пакет XmlSerDe.Compat. Проверяется
    /// главное свойство зависимости: анализатор обязан доехать транзитивно,
    /// иначе compat-код не сгенерируется и фасад молча отдаст всё штатному
    /// сериализатору - то есть ускорение исчезнет ровно там, ради чего слой
    /// и существует.
    /// </summary>
    public static class Program
    {
        /// <summary>Точка входа.</summary>
        public static void Main()
        {
            var serializer = new XmlSerDe.Compat.XmlSerializer(typeof(Order));
            Console.WriteLine("ACCELERATED: " + serializer.IsAccelerated);

            var writer = new StringWriter();
            serializer.Serialize(writer, new Order { Id = 7 });
            var xml = writer.ToString();

            var back = (Order?)serializer.Deserialize(new StringReader(xml));
            Console.WriteLine("BACK: Id=" + (back is null ? "null" : back.Id.ToString()));
        }
    }
}
