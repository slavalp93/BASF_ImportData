using System;
using System.Collections.Generic;
using NLog;
using System.Linq;
using Sungero.Domain.Client;
using Sungero.Domain.ClientLinqExpressions;

namespace ImportData
{
  class BusinessUnit : Entity
  {
    public int PropertiesCount = 19;
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
        var name = this.Parameters[shift + 0].Trim();
        if (string.IsNullOrEmpty(name))
        {
          var message = string.Format("Не заполнено поле \"Наименование\".");
          exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = "Error", Message = message});
          logger.Error(message);
          return exceptionList;
        }
        var legalName = this.Parameters[shift + 1].Trim();
        var headCompany = BusinessLogic.GetBusinessUnit(session, this.Parameters[shift + 2].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 2].Trim()) && headCompany == null)
        {
          headCompany = BusinessLogic.CreateBusinessUnit(session, this.Parameters[shift + 2].Trim(), exceptionList, logger);
          //var message = string.Format("Не найден Головная организация \"{1}\". Наименование НОР: \"{0}\". ", name, this.Parameters[shift + 2].Trim());
          //exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          //logger.Warn(message);
        }
        var ceo = BusinessLogic.GetEmployee(session, this.Parameters[shift + 3].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 3].Trim()) && ceo == null)
        {
          var message = string.Format("Не найден Руководитель \"{1}\". Наименование НОР: \"{0}\". ", name, this.Parameters[shift + 3].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }
        var tin = this.Parameters[shift + 4].Trim();
        var trrc = this.Parameters[shift + 5].Trim();
        var psrn = this.Parameters[shift + 6].Trim();
        var nceo = this.Parameters[shift + 7].Trim();
        var ncea = this.Parameters[shift + 8].Trim();
        var city = BusinessLogic.GetCity(session, this.Parameters[shift + 9].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 9].Trim()) && city == null)
        {
          var message = string.Format("Не найден Населенный пункт \"{1}\". Наименование НОР: \"{0}\". ", name, this.Parameters[shift + 9].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }
        var region = BusinessLogic.GetRegion(session, this.Parameters[shift + 10].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 10].Trim()) && region == null)
        {
          var message = string.Format("Не найден Регион \"{1}\". Наименование НОР: \"{0}\". ", name, this.Parameters[shift + 10].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }
        var legalAdress = this.Parameters[shift + 11].Trim();
        var postalAdress = this.Parameters[shift + 12].Trim();
        var phones = this.Parameters[shift + 13].Trim();
        var email = this.Parameters[shift + 14].Trim();
        var homepage = this.Parameters[shift + 15].Trim();
        var note = this.Parameters[shift + 16].Trim();
        var account = this.Parameters[shift + 17].Trim();
        var bank = BusinessLogic.GetBank(session, this.Parameters[shift + 18].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 18]) && bank == null)
        {
          var message = string.Format("Не найден Банк \"{1}\". Наименование НОР: \"{0}\". ", name, this.Parameters[shift + 18].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }

        try
        {
          var businessUnit = Sungero.Company.BusinessUnits.Null;
          if (ignoreDuplicates.ToLower() != Constants.ignoreDuplicates.ToLower())
          {
            var businessUnits = Enumerable.ToList(session.GetAll<Sungero.Company.IBusinessUnit>().Where(x => x.Name == name ||
                        !string.IsNullOrEmpty(tin) && x.TIN == tin ||
                        !string.IsNullOrEmpty(psrn) && x.PSRN == psrn));
            businessUnit = (Enumerable.FirstOrDefault<Sungero.Company.IBusinessUnit>(businessUnits));
            if (businessUnit != null)
            {
              if (!supplementEntity)
              {
                var message = string.Format("НОР не может быть импортирована. Найден дубль по реквизитам Наименование: \"{0}\", ИНН: {1}, ОГРН: {2}.", name, tin, psrn);
                exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
                logger.Error(message);
                return exceptionList;
              }
            }
          }
          businessUnit = session.Create<Sungero.Company.IBusinessUnit>();
          businessUnit.Name = name;
          businessUnit.LegalName = legalName;
          businessUnit.HeadCompany = headCompany;
          businessUnit.CEO = ceo;
          businessUnit.TIN = tin;
          businessUnit.TRRC = trrc;
          businessUnit.PSRN = psrn;
          businessUnit.NCEO = nceo;
          businessUnit.NCEA = ncea;
          businessUnit.City = city;
          businessUnit.Region = region;
          businessUnit.LegalAddress = legalAdress;
          businessUnit.PostalAddress = postalAdress;
          businessUnit.Phones = phones;
          businessUnit.Email = email;
          businessUnit.Homepage = homepage;
          businessUnit.Note = note;
          businessUnit.Account = account;
          businessUnit.Bank = bank;
          businessUnit.Save();
        }
        catch (Exception ex)
        {
          exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = Constants.ErrorTypes.Error, Message = ex.Message});
          return exceptionList;
        }
        session.SubmitChanges();
      }
      return exceptionList;
    }
  }
}
