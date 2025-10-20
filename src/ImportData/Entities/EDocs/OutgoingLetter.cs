using System;
using System.Collections.Generic;
using System.Globalization;
using NLog;
using System.Linq;
using Sungero.Domain.Client;
using Sungero.Domain.ClientLinqExpressions;

namespace ImportData
{
  class OutgoingLetter : Entity
  {
    public int PropertiesCount = 9;
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
        var regDate = DateTime.MinValue;
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

        var counterparty = BusinessLogic.GetConterparty(session, this.Parameters[shift + 2], exceptionList, logger);
        if (counterparty == null)
        {
          var message = string.Format("Не найден контрагент \"{0}\".", this.Parameters[shift + 2]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var documentKind = BusinessLogic.GetDocumentKind(session, this.Parameters[shift + 3], exceptionList, logger);
        if (documentKind == null)
        {
          var message = string.Format("Не найден вид документа \"{0}\".", this.Parameters[shift + 3]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var subject = this.Parameters[shift + 4];

        var department = BusinessLogic.GetDepartment(session, this.Parameters[shift + 5], null, exceptionList, logger);
        if (department == null)
        {
          var message = string.Format("Не найдено подразделение \"{0}\".", this.Parameters[shift + 5]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var preparedBy = BusinessLogic.GetEmployee(session, this.Parameters[shift + 6].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 6].Trim()) && preparedBy == null)
        {
          var message = string.Format("Не найден Подготавливающий \"{2}\". Исходящее письмо: \"{0} {1}\". ", regNumber, regDate.ToString(), this.Parameters[shift + 6].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }

        var filePath = this.Parameters[shift + 7];

        var note = this.Parameters[shift + 8];
        try
        {
          var outgoingLetter = Sungero.RecordManagement.OutgoingLetters.Null;

          var outgoingLetters = Enumerable.ToList(session.GetAll<Sungero.RecordManagement.IOutgoingLetter>().Where(x => x.RegistrationNumber == regNumber && regDate != DateTime.MinValue && x.RegistrationDate == regDate));
          outgoingLetter = (Enumerable.FirstOrDefault<Sungero.RecordManagement.IOutgoingLetter>(outgoingLetters));

          if (outgoingLetter == null)
            outgoingLetter = session.Create<Sungero.RecordManagement.IOutgoingLetter>();

          if (outgoingLetter != null && outgoingLetter.RegistrationState == Sungero.Docflow.OfficialDocument.RegistrationState.Registered)
          {
            outgoingLetter.State.Properties.RegistrationNumber.IsRequired = false;
            Sungero.Docflow.PublicFunctions.OfficialDocument.RegisterDocument(outgoingLetter, null, null, null, null, false);
            outgoingLetter.RegistrationDate = DateTime.Today;
            outgoingLetter.Save();
            session.SubmitChanges();
          }

          outgoingLetter.Correspondent = counterparty;
          if (regDate != DateTime.MinValue)
            outgoingLetter.RegistrationDate = regDate;
          outgoingLetter.RegistrationNumber = regNumber;
          outgoingLetter.DocumentKind = documentKind;
          outgoingLetter.Subject = subject;
          outgoingLetter.Department = department;
          if (department != null)
            outgoingLetter.BusinessUnit = department.BusinessUnit;
          outgoingLetter.PreparedBy = preparedBy;
          outgoingLetter.Note = note;
          outgoingLetter.Save();
          if (!string.IsNullOrWhiteSpace(filePath))
            exceptionList.Add(BusinessLogic.ImportBody(session, outgoingLetter, filePath, logger));
          var documentRegisterId = 0;
          if (ExtraParameters.ContainsKey("doc_register_id"))
            if (int.TryParse(ExtraParameters["doc_register_id"], out documentRegisterId))
              exceptionList.AddRange(BusinessLogic.RegisterDocument(session, outgoingLetter, documentRegisterId, regNumber, regDate, Constants.RolesGuides.RoleOutgoingDocumentsResponsible, logger));
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
