using System;
using System.Collections.Generic;
using System.Globalization;
using NLog;
using System.Linq;
using Sungero.Domain.Client;
using Sungero.Domain.ClientLinqExpressions;

namespace ImportData
{
  public class Contract : Entity
  {
    public int PropertiesCount = 17;
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
        DateTime? regDate = DateTime.MinValue;
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

        var contractCategory = BusinessLogic.GetContractCategory(session, this.Parameters[shift + 4], exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 4].ToString()))
        {
          if (contractCategory == null)
          {
            var message = string.Format("Не найдена категория договора \"{0}\".", this.Parameters[shift + 4]);
            exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
            logger.Error(message);
            return exceptionList;
          }
        }

        var subject = this.Parameters[shift + 5];
        var businessUnit = BusinessLogic.GetBusinessUnit(session, this.Parameters[shift + 6], exceptionList, logger);
        if (businessUnit == null)
        {
          var message = string.Format("Не найдена НОР \"{0}\".", this.Parameters[shift + 6]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }
        var department = BusinessLogic.GetDepartment(session, this.Parameters[shift + 7], businessUnit.Id, exceptionList, logger);
        if (department == null)
        {
          var message = string.Format("Не найдено подразделение \"{0}\".", this.Parameters[shift + 7]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }
        var filePath = this.Parameters[shift + 8];

        DateTime? validFrom = DateTime.MinValue;
        try
        {
          validFrom = ParseDate(this.Parameters[shift + 9], style, culture);
        }
        catch (Exception)
        {
          var message = string.Format("Не удалось обработать значение в поле \"Действует с\" \"{0}\".", this.Parameters[shift + 9]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        DateTime? validTill = DateTime.MinValue;
        try
        {
          validTill = ParseDate(this.Parameters[shift + 10], style, culture);
        }
        catch (Exception)
        {
          var message = string.Format("Не удалось обработать значение в поле \"Действует по\" \"{0}\".", this.Parameters[shift + 10]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var totalAmount = 0.0;
        if (!string.IsNullOrWhiteSpace(this.Parameters[shift + 11]) && !double.TryParse(this.Parameters[shift + 11].Trim(), style, culture, out totalAmount))
        {
          var message = string.Format("Не удалось обработать значение в поле \"Сумма\" \"{0}\".", this.Parameters[shift + 11]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }

        var currency = BusinessLogic.GetCurrency(session, this.Parameters[shift + 12], exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 12].Trim()) && currency == null)
        {
          var message = string.Format("Не найдено соответствующее наименование валюты \"{0}\".", this.Parameters[shift + 12]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }
        var lifeCycleState = BusinessLogic.GetPropertyLifeCycleState(session, this.Parameters[shift + 13]);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 13].Trim()) && lifeCycleState == null)
        {
          var message = string.Format("Не найдено соответствующее значение состояния \"{0}\".", this.Parameters[shift + 13]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }
        var responsibleEmployee = BusinessLogic.GetEmployee(session, this.Parameters[shift + 14].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 14].Trim()) && responsibleEmployee == null)
        {
          var message = string.Format("Не найден Ответственный \"{3}\". Договор: \"{0} {1} {2}\". ", regNumber, regDate.ToString(), counterparty, this.Parameters[shift + 14].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }
        var ourSignatory = BusinessLogic.GetEmployee(session, this.Parameters[shift + 15].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 15].Trim()) && ourSignatory == null)
        {
          var message = string.Format("Не найден Подписывающий \"{3}\". Договор: \"{0} {1} {2}\". ", regNumber, regDate.ToString(), counterparty, this.Parameters[shift + 15].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }
        var note = this.Parameters[shift + 16];
        try
        {
          var contracts = Enumerable.ToList(session.GetAll<Sungero.Contracts.IContract>().Where(x => Equals(x.RegistrationNumber, regNumber) &&
                                                                                              Equals(x.RegistrationDate, regDate) &&
                                                                                              Equals(x.Counterparty, counterparty)));
          var contract = (Enumerable.FirstOrDefault<Sungero.Contracts.IContract>(contracts));
          if (contract == null)
            contract = session.Create<Sungero.Contracts.IContract>();

          if (contract != null && contract.RegistrationState == Sungero.Docflow.OfficialDocument.RegistrationState.Registered)
          {
            contract.State.Properties.RegistrationNumber.IsRequired = false;
            Sungero.Docflow.PublicFunctions.OfficialDocument.RegisterDocument(contract, null, null, null, null, false);
            contract.RegistrationDate = DateTime.Today;
            contract.Save();
            session.SubmitChanges();
          }

          contract.Counterparty = counterparty;
          contract.DocumentKind = documentKind;
          contract.DocumentGroup = contractCategory;
          contract.Subject = subject;
          contract.BusinessUnit = businessUnit;
          contract.Department = department;
          if (validFrom != DateTime.MinValue)
            contract.ValidFrom = validFrom;
          if (validTill != DateTime.MinValue)
            contract.ValidTill = validTill;
          contract.TotalAmount = totalAmount;
          contract.Currency = currency;
          contract.LifeCycleState = lifeCycleState;
          contract.ResponsibleEmployee = responsibleEmployee;
          contract.OurSignatory = ourSignatory;
          contract.Note = note;
          if (regDate != DateTime.MinValue)
            contract.RegistrationDate = regDate;
          contract.RegistrationNumber = regNumber;
          contract.Save();
          if (!string.IsNullOrWhiteSpace(filePath))
            exceptionList.Add(BusinessLogic.ImportBody(session, contract, filePath, logger));
          var documentRegisterId = 0;
          if (ExtraParameters.ContainsKey("doc_register_id"))
            if (int.TryParse(ExtraParameters["doc_register_id"], out documentRegisterId))
              exceptionList.AddRange(BusinessLogic.RegisterDocument(session, contract, documentRegisterId, regNumber, regDate, Constants.RolesGuides.RoleContractResponsible, logger));
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
