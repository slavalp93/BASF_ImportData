using System;
using System.Collections.Generic;
using NLog;
using System.Linq;
using Sungero.Domain.Client;
using Sungero.Domain.ClientLinqExpressions;
using System.Globalization;

namespace ImportData
{
  class Addendum : Entity
  {
    public int PropertiesCount = 7;
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

      var addendum = Sungero.Docflow.Addendums.Null;
      var regDateLeadingDocument = DateTime.MinValue;
      var regNumberLeadingDocument = string.Empty;
      var counterparty = Sungero.Parties.Companies.Null;
      var documentKind = Sungero.Docflow.DocumentKinds.Null;
      var subject = string.Empty;
      Sungero.Core.Enumeration? lifeCycleState;
      var note = string.Empty;
      var leadingDocument = Sungero.Docflow.OfficialDocuments.Null;
      var filePath = string.Empty;

      try
      {
        using (var session = new Session())
        {
          regNumberLeadingDocument = this.Parameters[shift + 0];
          try
          {
            regDateLeadingDocument = ParseDate(this.Parameters[shift + 1], NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CreateSpecificCulture("en-GB"));
          }
          catch (Exception)
          {
            var message = string.Format("Не удалось обработать дату ведущего документа \"{0}\".", this.Parameters[shift + 1]);
            exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
            logger.Error(message);
            return exceptionList;
          }

          documentKind = BusinessLogic.GetDocumentKind(session, this.Parameters[shift + 2], exceptionList, logger);
          if (documentKind == null)
          {
            var message = string.Format("Не найден вид документа \"{0}\".", this.Parameters[shift + 2]);
            exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
            logger.Error(message);
            return exceptionList;
          }

          subject = this.Parameters[shift + 3];

          filePath = this.Parameters[shift + 4];

          lifeCycleState = BusinessLogic.GetPropertyLifeCycleState(session, this.Parameters[shift + 5]);
          if (!string.IsNullOrEmpty(this.Parameters[shift + 5].Trim()) && lifeCycleState == null)
          {
            var message = string.Format("Не найдено соответствующее значение состояния \"{0}\".", this.Parameters[shift + 5]);
            exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
            logger.Error(message);
            return exceptionList;
          }

          note = this.Parameters[shift + 6];

          var documents = Enumerable.ToList(session.GetAll<Sungero.Docflow.IOfficialDocument>().Where(x => x.RegistrationNumber == regNumberLeadingDocument &&
                                                                                                      x.RegistrationDate == regDateLeadingDocument));
          if (documents.Count() > 1)
          {
            var message = string.Format("Приложение не может быть импортировано. Найдено несколько ведущих документов с такими же реквизитами \"Дата документа\" {0}, \"Рег. №\" {1}.", regDateLeadingDocument.ToString("d"), regNumberLeadingDocument);
            exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
            logger.Error(message);
            return exceptionList;
          }

          leadingDocument = (Enumerable.FirstOrDefault<Sungero.Docflow.IOfficialDocument>(documents));
          if (leadingDocument == null)
          {
            var message = string.Format("Приложение не может быть импортировано. Не найден ведущий документ с реквизитами \"Дата документа\" {0}, \"Рег. №\" {1}.", regDateLeadingDocument.ToString("d"), regNumberLeadingDocument);
            exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
            logger.Error(message);
            return exceptionList;
          }

          var addendums = Enumerable.ToList(session.GetAll<Sungero.Docflow.IAddendum>().Where(x => Equals(x.LeadingDocument, leadingDocument) && Equals(x.DocumentKind, documentKind) && x.Subject == subject));
          addendum = (Enumerable.FirstOrDefault<Sungero.Docflow.IAddendum>(addendums));

          // HACK: Создаем 2 сессии. В первой загружаем данные, во второй создаем объект системы.
          if (addendum == null)
            addendum = session.Create<Sungero.Docflow.IAddendum>();

          if (addendum != null && addendum.RegistrationState == Sungero.Docflow.OfficialDocument.RegistrationState.Registered)
          {
            addendum.State.Properties.RegistrationNumber.IsRequired = false;
            Sungero.Docflow.PublicFunctions.OfficialDocument.RegisterDocument(addendum, null, null, null, null, false);
            addendum.RegistrationDate = DateTime.Today;
            addendum.Save();
            session.SubmitChanges();
          }

          session.Clear();
          session.Dispose();
        }

        addendum.LeadingDocument = leadingDocument;

        using (var session = new Session())
        {
          try
          {
            addendum.DocumentKind = documentKind;
            addendum.Subject = subject;
            addendum.LifeCycleState = lifeCycleState;
            addendum.Note = note;
            addendum.Save();
            if (!string.IsNullOrWhiteSpace(filePath))
            {
              var importBody = BusinessLogic.ImportBody(session, addendum, filePath, logger);
              if (importBody.ErrorType != null || importBody.Message != null)
              {
                exceptionList.Add(importBody);
                var message = string.Format("Приложение не может быть импортировано. Ошибка при создании тела документа \"Файл\" {0}.", filePath);
                exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
                logger.Error(message);
                return exceptionList;
              }
            }
            var documentRegisterId = 0;
            if (ExtraParameters.ContainsKey("doc_register_id"))
              if (int.TryParse(ExtraParameters["doc_register_id"], out documentRegisterId))
                exceptionList.AddRange(BusinessLogic.RegisterDocument(session, addendum, documentRegisterId, regNumberLeadingDocument, regDateLeadingDocument, Constants.RolesGuides.RoleContractResponsible, logger));
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
            exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = ex.Message });
            logger.Error(ex.Message);
          }
          try
          {
            session.SubmitChanges();
          }
          catch (Exception ex)
          {
            exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = ex.Message });
            logger.Error(ex.Message);
            session.Clear();
            session.Dispose();
          }
        }
      }
      catch (Exception ex)
      {
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = ex.Message });
        return exceptionList;
      }
      return exceptionList;
    }
  }
}
