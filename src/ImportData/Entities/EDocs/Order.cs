using System;
using System.Collections.Generic;
using System.Globalization;
using NLog;
using System.Linq;
using Sungero.Domain.Client;
using Sungero.Domain.ClientLinqExpressions;

namespace ImportData
{
  class Order : Entity
  {
    public int PropertiesCount = 12;
    /// <summary>
    /// Получить наименование число запрашиваемых параметров.
    /// </summary>
    /// <returns>Число запрашиваемых параметров.</returns>
    public override int GetPropertiesCount()
    {
      return PropertiesCount;
    }

    /// <summary>
    /// Сохранение сущности в RX.
    /// </summary>
    /// <param name="shift">Сдвиг по горизонтали в XLSX документе. Необходим для обработки документов, составленных из элементов разных сущностей.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Число запрашиваемых параметров.</returns>
    public override IEnumerable<Structures.ExceptionsStruct> SaveToRX(NLog.Logger logger, bool supplementEntity, string ignoreDuplicates, int shift = 0)
    {
      var exceptionList = new List<Structures.ExceptionsStruct>();

      using (var session = new Session())
      {
        var regNumber = this.Parameters[shift + 0];
        DateTime? regDate = null;
        var style = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;
        var culture = CultureInfo.CreateSpecificCulture("en-GB");
        try
        {
          regDate = ParseDate(this.Parameters[shift + 1], style, culture);
        }
        catch (Exception)
        {
          var message = string.Format("Не удалось обработать дату регистрации \"{0}\".", this.Parameters[shift + 1]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var documentKind = BusinessLogic.GetDocumentKind(session, this.Parameters[shift + 2], exceptionList, logger);
        if (documentKind == null)
        {
          var message = string.Format("Не найден вид документа \"{0}\".", this.Parameters[shift + 2]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var subject = this.Parameters[shift + 3];

        var businessUnit = BusinessLogic.GetBusinessUnit(session, this.Parameters[shift + 4], exceptionList, logger);
        if (businessUnit == null)
        {
          var message = string.Format("Не найдена НОР \"{0}\".", this.Parameters[shift + 4]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var department = BusinessLogic.GetDepartment(session, this.Parameters[shift + 5], null, exceptionList, logger);
        if (department == null)
        {
          var message = string.Format("Не найдено подразделение \"{0}\".", this.Parameters[shift + 5]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var filePath = this.Parameters[shift + 6];

        var assignee = BusinessLogic.GetEmployee(session, this.Parameters[shift + 7].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 7].Trim()) && assignee == null)
        {
          var message = string.Format("Не найден Исполнитель \"{2}\". Приказ: \"{0} {1}\". ", regNumber, regDate.ToString(), this.Parameters[shift + 7].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }

        var preparedBy = BusinessLogic.GetEmployee(session, this.Parameters[shift + 8].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 8].Trim()) && preparedBy == null)
        {
          var message = string.Format("Не найден Подготавливающий \"{2}\". Приказ: \"{0} {1}\". ", regNumber, regDate.ToString(), this.Parameters[shift + 8].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var ourSignatory = BusinessLogic.GetEmployee(session, this.Parameters[shift + 9].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 9].Trim()) && ourSignatory == null)
        {
          var message = string.Format("Не найден Подписывающий \"{2}\". Приказ: \"{0} {1}\". ", regNumber, regDate.ToString(), this.Parameters[shift + 9].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }

        var lifeCycleState = BusinessLogic.GetPropertyLifeCycleState(session, this.Parameters[shift + 10]);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 10].Trim()) && lifeCycleState == null)
        {
          var message = string.Format("Не найдено соответствующее значение состояния \"{0}\".", this.Parameters[shift + 10]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var note = this.Parameters[shift + 11];
        try
        {
          var order = Sungero.RecordManagement.Orders.Null;
          var orders = Enumerable.ToList(session.GetAll<Sungero.RecordManagement.IOrder>().Where(x => x.RegistrationNumber == regNumber && regDate != DateTime.MinValue && x.RegistrationDate == regDate));
          order = (Enumerable.FirstOrDefault<Sungero.RecordManagement.IOrder>(orders));

          if (order == null)
            order = session.Create<Sungero.RecordManagement.IOrder>();

          if (order != null && order.RegistrationState == Sungero.Docflow.OfficialDocument.RegistrationState.Registered)
          {
            order.State.Properties.RegistrationNumber.IsRequired = false;
            Sungero.Docflow.PublicFunctions.OfficialDocument.RegisterDocument(order, null, null, null, null, false);
            order.RegistrationDate = DateTime.Today;
            order.Save();
            session.SubmitChanges();
          }

          if (regDate != null)
            order.RegistrationDate = regDate;
          order.RegistrationNumber = regNumber;
          order.DocumentKind = documentKind;
          order.Subject = subject;
          order.BusinessUnit = businessUnit;
          order.Department = department;
          order.Assignee = assignee;
          order.PreparedBy = preparedBy;
          order.OurSignatory = ourSignatory;
          order.LifeCycleState = lifeCycleState;
          order.Note = note;
          order.Save();
          if (!string.IsNullOrWhiteSpace(filePath))
            exceptionList.Add(BusinessLogic.ImportBody(session, order, filePath, logger));
          var documentRegisterId = 0;
          if (ExtraParameters.ContainsKey("doc_register_id"))
            if (int.TryParse(ExtraParameters["doc_register_id"], out documentRegisterId))
              exceptionList.AddRange(BusinessLogic.RegisterDocument(session, order, documentRegisterId, regNumber, regDate, Constants.RolesGuides.RoleIncomingDocumentsResponsible, logger));
            else
            {
              var message = string.Format("Не удалось обработать параметр \"doc_register_id\". Полученное значение: {0}.", ExtraParameters["doc_register_id"]);
              exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
              logger.Error(message);
              return exceptionList;
            }
        }
        catch (Exception ex)
        {
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = ex.Message });
          return exceptionList;
        }
        session.SubmitChanges();
      }
      return exceptionList;
    }
  }
}
