using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace ImportData
{
  public class Entity
  {
    public string[] Parameters;
    public Dictionary<string, string> ExtraParameters;
    public int PropertiesCount = 0;

    /// <summary>
    /// Получить наименование число запрашиваемых параметров.
    /// </summary>
    /// <returns>Число запрашиваемых параметров.</returns>
    public virtual int GetPropertiesCount()
    {
      return PropertiesCount;
    }

    /// <summary>
    /// Сохранение сущности в RX.
    /// </summary>
    /// <param name="logger">Логировщик.</param>
    /// <param name="shift">Сдвиг по горизонтали в XLSX документе. Необходим для обработки документов, составленных из элементов разных сущностей.</param>
    /// <returns>Список ошибок.</returns>
    public virtual IEnumerable<Structures.ExceptionsStruct> SaveToRX(NLog.Logger logger, bool supplementEntity, string ignoreDuplicates, int shift = 0)
    {
      return new List<Structures.ExceptionsStruct>();
    }

    /// <summary>
    /// Преобразовать значение в дату.
    /// </summary>
    /// <param name="value">Значение.</param>
    /// <param name="style">Стиль преобразования числовой строки.</param>
    /// <param name="culture">Культура.</param>
    /// <returns>Преобразованная дата.</returns>
    /// <exception cref="FormatException" />
    public DateTime ParseDate(string value, NumberStyles style, CultureInfo culture)
    {
      if (!string.IsNullOrEmpty(value))
      {
        DateTime date;
        if (DateTime.TryParse(value.Trim(), culture.DateTimeFormat, DateTimeStyles.None, out date))
          return date;

        var dateDouble = 0.0;
        if (double.TryParse(value.Trim(), style, culture, out dateDouble))
          return DateTime.FromOADate(dateDouble);

        throw new FormatException("Неверный формат строки.");
      }
      else
        return DateTime.MinValue;
      
    }
  }
}
