using System;
using System.Xml.Serialization;

namespace XmlSerDe.Compat
{
    /// <summary>
    /// Замена <see cref="System.Xml.Serialization.XmlSerializerFactory"/> - вторая
    /// строка, которую имеет смысл написать проекту, если он пользуется фабрикой,
    /// а не конструктором:
    ///
    /// <code>global using XmlSerializerFactory = XmlSerDe.Compat.XmlSerializerFactory;</code>
    ///
    /// Без неё <c>factory.CreateSerializer(typeof(T))</c> проходит мимо фасада
    /// целиком: возвращается штатный сериализатор, всё работает, ускорения нет
    /// и никакого признака почему.
    ///
    /// <b>Методы перекрыты через <c>new</c>, а не override</b> - у базовой фабрики
    /// ни один <c>CreateSerializer</c> не виртуальный (проверено рефлексией).
    /// Значит работает это там, где статический тип в точке вызова - наша фабрика,
    /// то есть ровно в сценарии подмены одной строкой; через ссылку базового типа
    /// вызов уйдёт мимо, как и раньше.
    ///
    /// Незарегистрированный тип отдаётся <b>базовой фабрике</b>, а не заворачивается
    /// в фасад: фабрика существует ради кэша, и терять его на типах, которым фасад
    /// всё равно ничем не поможет, незачем. Поэтому подмена здесь либо ускоряет,
    /// либо не меняет ровно ничего.
    /// </summary>
    public class XmlSerializerFactory : System.Xml.Serialization.XmlSerializerFactory
    {
        public new System.Xml.Serialization.XmlSerializer CreateSerializer(Type type)
        {
            if (type is null || !XmlSerDeRegistry.IsRegistered(type))
            {
                //null тоже уходит базовой фабрике: пусть исключение про него
                //бросит она, ровно то же, что и до подмены
                return base.CreateSerializer(type!);
            }

            return new XmlSerializer(type);
        }

        //перегрузки с добавочным аргументом ускорить нечем (см. конструкторы фасада),
        //поэтому они уходят базовой фабрике целиком - вместе с её кэшем
        public new System.Xml.Serialization.XmlSerializer CreateSerializer(Type type, string? defaultNamespace)
        {
            if (string.IsNullOrEmpty(defaultNamespace))
            {
                return CreateSerializer(type);
            }

            return base.CreateSerializer(type, defaultNamespace);
        }

        public new System.Xml.Serialization.XmlSerializer CreateSerializer(Type type, Type[]? extraTypes)
        {
            if (extraTypes is null || extraTypes.Length == 0)
            {
                return CreateSerializer(type);
            }

            return base.CreateSerializer(type, extraTypes);
        }

        public new System.Xml.Serialization.XmlSerializer CreateSerializer(Type type, XmlAttributeOverrides? overrides)
        {
            if (overrides is null)
            {
                return CreateSerializer(type);
            }

            return base.CreateSerializer(type, overrides);
        }

        public new System.Xml.Serialization.XmlSerializer CreateSerializer(Type type, XmlRootAttribute? root)
        {
            if (root is null)
            {
                return CreateSerializer(type);
            }

            return base.CreateSerializer(type, root);
        }

        public new System.Xml.Serialization.XmlSerializer CreateSerializer(
            Type type,
            XmlAttributeOverrides? overrides,
            Type[]? extraTypes,
            XmlRootAttribute? root,
            string? defaultNamespace
            )
        {
            if (overrides is null
                && (extraTypes is null || extraTypes.Length == 0)
                && root is null
                && string.IsNullOrEmpty(defaultNamespace))
            {
                return CreateSerializer(type);
            }

            return base.CreateSerializer(type, overrides, extraTypes, root, defaultNamespace);
        }
    }
}
