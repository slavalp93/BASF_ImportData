using System;
using System.Collections.Generic;
using System.Globalization;
using NLog;
using System.Linq;
using Sungero.Domain.Client;
using Sungero.Domain.ClientLinqExpressions;

namespace ImportData
{
  class SupAgreement : Entity
  {
    public int PropertiesCount = 18;
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

        var regNumberLeadingDocument = this.Parameters[shift + 2];

        var regDateLeadingDocument = DateTime.MinValue;
        try
        {
          regDateLeadingDocument = ParseDate(this.Parameters[shift + 3], style, culture);
        }
        catch (Exception)
        {
          var message = string.Format("Не удалось обработать дату регистрации ведущего документа \"{0}\".", this.Parameters[shift + 3]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var counterparty = BusinessLogic.GetConterparty(session, this.Parameters[shift + 4], exceptionList, logger);
        if (counterparty == null)
        {
          var message = string.Format("Не найден контрагент \"{0}\".", this.Parameters[shift + 4]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var documentKind = BusinessLogic.GetDocumentKind(session, this.Parameters[shift + 5], exceptionList, logger);
        if (documentKind == null)
        {
          var message = string.Format("Не найден вид документа \"{0}\".", this.Parameters[shift + 5]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var subject = this.Parameters[shift + 6];

        var businessUnit = BusinessLogic.GetBusinessUnit(session, this.Parameters[shift + 7], exceptionList, logger);
        if (businessUnit == null)
        {
          var message = string.Format("Не найдена НОР \"{0}\".", this.Parameters[shift + 7]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var department = BusinessLogic.GetDepartment(session, this.Parameters[shift + 8], null, exceptionList, logger);
        if (department == null)
        {
          var message = string.Format("Не найдено подразделение \"{0}\".", this.Parameters[shift + 8]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var filePath = this.Parameters[shift + 9];

        var validFrom = DateTime.MinValue;
        try
        {
          validFrom = ParseDate(this.Parameters[shift + 10], style, culture);
        }
        catch (Exception)
        {
          var message = string.Format("Не удалось обработать значение в поле \"Действует с\" \"{0}\".", this.Parameters[shift + 10]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var validTill = DateTime.MinValue;
        try
        {
          validTill = ParseDate(this.Parameters[shift + 11], style, culture);
        }
        catch (Exception)
        {
          var message = string.Format("Не удалось обработать значение в поле \"Действует по\" \"{0}\".", this.Parameters[shift + 11]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var totalAmount = 0.0;
        if (!string.IsNullOrWhiteSpace(this.Parameters[shift + 12]) && !double.TryParse(this.Parameters[shift + 12].Trim(), style, culture, out totalAmount))
        {
          var message = string.Format("Не удалось обработать значение в поле \"Сумма\" \"{0}\".", this.Parameters[shift + 12]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var currency = BusinessLogic.GetCurrency(session, this.Parameters[shift + 13], exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 13].Trim()) && currency == null)
        {
          var message = string.Format("Не найдено соответствующее наименование валюты \"{0}\".", this.Parameters[shift + 13]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }
        var lifeCycleState = BusinessLogic.GetPropertyLifeCycleState(session, this.Parameters[shift + 14]);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 14].Trim()) && lifeCycleState == null)
        {
          var message = string.Format("Не найдено соответствующее значение состояния \"{0}\".", this.Parameters[shift + 14]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }
        var responsibleEmployee = BusinessLogic.GetEmployee(session, this.Parameters[shift + 15].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 15].Trim()) && responsibleEmployee == null)
        {
          var message = string.Format("Не найден Ответственный \"{3}\". Доп. соглашение: \"{0} {1} {2}\". ", regNumber, regDate.ToString(), counterparty, this.Parameters[shift + 15].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }
        var ourSignatory = BusinessLogic.GetEmployee(session, this.Parameters[shift + 16].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 16].Trim()) && ourSignatory == null)
        {
          var message = string.Format("Не найден Подписывающий \"{3}\". Доп. соглашение: \"{0} {1} {2}\". ", regNumber, regDate.ToString(), counterparty, this.Parameters[shift + 16].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }
        var note = this.Parameters[shift + 17];
        try
        {
          var contracts = Enumerable.ToList(session.GetAll<Sungero.Contracts.IContract>()
            .Where(x => x.RegistrationDate == regDateLeadingDocument && x.RegistrationNumber == regNumberLeadingDocument && x.Counterparty.Equals(counterparty)));
          var leadingDocument = (Enumerable.FirstOrDefault<Sungero.Contracts.IContract>(contracts));
          if (leadingDocument == null)
          {
            var message = string.Format("Доп.соглашение не может быть импортировано. Не найден ведущий документ с реквизитами \"Дата документа\" {0}, \"Рег. №\" {1} и \"Контрагент\" {2}.", regDateLeadingDocument.ToString("d"), regNumberLeadingDocument, counterparty.Name);
            exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
            logger.Error(message);
            return exceptionList;
          }

          // HACK: Создаем 2 сессии. В первой загружаем данные, во второй создаем объект системы.

          var supAgreement = Sungero.Contracts.SupAgreements.Null;

          var supAgreements = Enumerable.ToList(session.GetAll<Sungero.Contracts.ISupAgreement>().Where(x => x.RegistrationNumber == regNumber));
          supAgreement = (Enumerable.FirstOrDefault<Sungero.Contracts.ISupAgreement>(supAgreements));

          if (supAgreement == null)
            supAgreement = session.Create<Sungero.Contracts.ISupAgreement>();

          if (supAgreement != null && supAgreement.RegistrationState == Sungero.Docflow.OfficialDocument.RegistrationState.Registered)
          {
            supAgreement.State.Properties.RegistrationNumber.IsRequired = false;
            Sungero.Docflow.PublicFunctions.OfficialDocument.RegisterDocument(supAgreement, null, null, null, null, false);
            supAgreement.RegistrationDate = DateTime.Today;
            supAgreement.Save();
            session.SubmitChanges();
          }

          session.Clear();
          session.Dispose();

          supAgreement.LeadingDocument = leadingDocument;


          using (var session1 = new Session())
          {
            supAgreement.Counterparty = counterparty;
            if (regDate != DateTime.MinValue)
              supAgreement.RegistrationDate = regDate;
            supAgreement.RegistrationNumber = regNumber;
            supAgreement.DocumentKind = documentKind;
            supAgreement.Subject = subject;
            supAgreement.BusinessUnit = businessUnit;
            supAgreement.Department = department;
            if (validFrom != DateTime.MinValue)
              supAgreement.ValidFrom = validFrom;
            if (validTill != DateTime.MinValue)
              supAgreement.ValidTill = validTill;
            supAgreement.TotalAmount = totalAmount;
            supAgreement.Currency = currency;
            supAgreement.LifeCycleState = lifeCycleState;
            supAgreement.ResponsibleEmployee = responsibleEmployee;
            supAgreement.OurSignatory = ourSignatory;
            supAgreement.Note = note;
            supAgreement.Save();
            session1.SubmitChanges();
          }
          if (!string.IsNullOrWhiteSpace(filePath))
            exceptionList.Add(BusinessLogic.ImportBody(session, supAgreement, filePath, logger));
          var documentRegisterId = 0;
          if (ExtraParameters.ContainsKey("doc_register_id"))
            if (int.TryParse(ExtraParameters["doc_register_id"], out documentRegisterId))
              exceptionList.AddRange(BusinessLogic.RegisterDocument(session, supAgreement, documentRegisterId, regNumber, regDate, Constants.RolesGuides.RoleContractResponsible, logger));
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
      }
      return exceptionList;
    }
  }
}
