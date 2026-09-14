using System;
using XmlSerDe;
using XmlSerDe.Tests.Complex;
using XmlSerDe.Tests.Complex.Subject;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Отрицательный фильтр перед разбором <c>xsi:type</c>
    /// (<c>XmlHead.GetXsiTypePrefiltered</c>, docs README «Cost of the xsi:type
    /// lookup»). Генератор ставит его типу без наследников: там атрибут ничего
    /// не диспетчеризует, в документах его обычно нет, и промах фильтра
    /// экономит полный обход головы.
    ///
    /// Фильтр обязан быть строгим ровно в одну сторону: «подстроки нет - и
    /// атрибута нет» должно быть верно всегда, а «подстрока есть» не обязано
    /// значить ничего. Обычные round-trip тесты этого не стерегут: они
    /// проверяют документы, где <c>xsi:type</c> либо честно стоит, либо честно
    /// отсутствует, и не отличили бы правильный фильтр от такого, который
    /// молча съедает атрибут. Здесь стоят оба края.
    /// </summary>
    public class XsiTypePrefilterFixture
    {
        #region фильтр не съедает настоящий xsi:type

        [Fact]
        public void UnknownXsiType_OnTypeWithoutDerived_StillThrows_Test()
        {
            //у XmlObject2 наследников нет, значит голова читается
            //через фильтр - и он обязан пропустить атрибут к разбору,
            //иначе документ с чужим типом прошёл бы молча
            var exception = Assert.Throws<InvalidOperationException>(() =>
                XmlSerializerDeserializer2.Deserialize(
                    DefaultInjector.Instance,
                    "<XmlObject2 xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:type=\"Bogus\"><IntProperty>7</IntProperty></XmlObject2>".AsSpan(),
                    out XmlObject2 _
                    )
                );

            Assert.Contains("Bogus", exception.Message);
        }

        [Fact]
        public void OwnXsiType_OnTypeWithoutDerived_IsAccepted_Test()
        {
            //xsi:type, называющий сам себя, легален: фильтр находит подстроку,
            //разбор находит атрибут, имя совпадает с объявленным
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                "<XmlObject2 xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:type=\"XmlObject2\"><IntProperty>7</IntProperty></XmlObject2>".AsSpan(),
                out XmlObject2 result
                );

            Assert.Equal(7, result.IntProperty);
        }

        [Fact]
        public void UnknownPrefixedType_OnFlexibleHost_StillThrows_Test()
        {
            //тот же край для GetPreciseNodeTypePrefiltered: префикс там
            //рантаймовый, фильтр ищет ":type", и пропустить атрибут обязан
            var exception = Assert.Throws<InvalidOperationException>(() =>
                XmlSerializerDeserializerFlexibleXsi.Deserialize(
                    DefaultInjector.Instance,
                    "<XmlObject2 xmlns:p3=\"http://www.w3.org/2001/XMLSchema-instance\" p3:type=\"Bogus\"><IntProperty>7</IntProperty></XmlObject2>".AsSpan(),
                    out XmlObject2 _
                    )
                );

            Assert.Contains("Bogus", exception.Message);
        }

        #endregion

        #region ложное срабатывание фильтра безвредно

        [Fact]
        public void XsiTypeInsideAttributeValue_IsNotMistakenForXsiType_Test()
        {
            //подстрока "xsi:type" лежит внутри значения чужого атрибута.
            //Фильтр срабатывает, разбор не находит атрибута с таким именем,
            //и нода остаётся собой - а не становится ошибкой разбора
            GuardDefaultHost.Deserialize(
                DefaultInjector.Instance,
                "<GuardSubject id=\"1\" tag=\"xsi:type\"><Title>t</Title></GuardSubject>".AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(1, result.Id);
            Assert.Equal("xsi:type", result.Tag);
        }

        [Fact]
        public void ColonTypeInsideAttributeValue_OnFlexibleHost_IsHarmless_Test()
        {
            //то же для фильтра по ":type"
            XmlSerializerDeserializerFlexibleXsi.Deserialize(
                DefaultInjector.Instance,
                "<XmlObject2><StringProperty>p3:type</StringProperty><IntProperty>7</IntProperty></XmlObject2>".AsSpan(),
                out XmlObject2 result
                );

            Assert.Equal("p3:type", result.StringProperty);
            Assert.Equal(7, result.IntProperty);
        }

        #endregion

        #region тип с наследниками фильтра не получает

        [Fact]
        public void PolymorphicDispatch_StillWorks_Test()
        {
            //BaseInfo наследников имеет, поэтому у него остаётся
            //нефильтрованный GetXsiType - здесь проверяется, что разделение
            //по наличию наследников не сломало саму диспетчеризацию
            GuardLadderDefault.Deserialize(
                DefaultInjector.Instance,
                ComplexFixture.AuxXml.AsSpan(),
                out InfoContainer result
                );

            Assert.Equal(3, result.InfoCollection!.Count);
            Assert.IsType<Derived3Info>(result.InfoCollection[0]);
            Assert.IsType<Derived1Info>(result.InfoCollection[1]);
            Assert.IsType<Derived2Info>(result.InfoCollection[2]);
        }

        #endregion
    }
}
