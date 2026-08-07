#nullable disable

using System.Collections.Generic;

namespace XmlSerDe.Tests.Interop.Subject
{
    /// <summary>
    /// Ребёнок для форм, которым нужен вложенный сложный тип.
    /// </summary>
    public class ChildSubject
    {
        public int Number { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Вложенность: заполненный ребёнок и null-ребёнок.
    /// </summary>
    public class NestedSubject
    {
        public ChildSubject Filled { get; set; }
        public ChildSubject Empty { get; set; }
        public int After { get; set; }
    }

    /// <summary>
    /// <c>List&lt;T&gt;</c> примитивов и сложных типов.
    /// </summary>
    public class ListSubject
    {
        public List<int> Numbers { get; set; }
        public List<string> Strings { get; set; }
        public List<ChildSubject> Children { get; set; }
    }

    /// <summary>
    /// <c>T[]</c> - вторая (и последняя) поддерживаемая XmlSerDe коллекция.
    /// </summary>
    public class ArraySubject
    {
        public int[] Numbers { get; set; }
        public string[] Strings { get; set; }
        public ChildSubject[] Children { get; set; }
    }

    /// <summary>
    /// Пустая коллекция против отсутствующей: у BCL это <c>&lt;X /&gt;</c> против
    /// пропуска члена, и различие значащее - оно переживает round-trip.
    /// </summary>
    public class EmptyCollectionsSubject
    {
        public List<int> EmptyList { get; set; }
        public List<int> NullList { get; set; }
        public int[] EmptyArray { get; set; }
        public int[] NullArray { get; set; }
    }

    /// <summary>
    /// Коллекция без сеттера, инициализированная в конструкторе. BCL такое поддерживает
    /// (он зовёт Add у уже существующего экземпляра), XmlSerDe требует сеттер и молча
    /// пропускает такой член - распространённый в реальных DTO паттерн.
    /// </summary>
    public class GetOnlyCollectionSubject
    {
        public GetOnlyCollectionSubject()
        {
            Items = new List<string>();
        }

        public List<string> Items { get; }

        /// <summary>
        /// Сеттера нет, и конструктор экземпляр не создал: наполнять нечего.
        /// Обе стороны такой член молча пропускают - создавать список
        /// за пользователя не станет ни одна.
        /// </summary>
        public List<string> NeverCreated { get; }

        public int Other { get; set; }
    }
}
