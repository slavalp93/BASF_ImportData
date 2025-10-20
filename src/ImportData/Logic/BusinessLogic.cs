using System;
using System.Linq;
using Sungero.Domain.Client;
using Sungero.Domain.Shared;
using Sungero.Domain.Client.Deployment;
using Sungero.Metadata.Services;
using CommonLibrary.Dependencies;
using Sungero.Domain.ClientLinqExpressions;
using Sungero.Domain.ClientBase;
using Sungero.Presentation;
using System.Collections.Generic;
using NLog;
using System.IO;
using Sungero.Core;

namespace ImportData
{
  public class BusinessLogic
  {
    public static IEnumerable<string> ErrorList;

    #region Работа с документами.
    /// <summary>
    /// Импорт тела документа.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="edoc">Экземпляр документа.</param>
    /// <param name="pathToBody">Путь к телу документа.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Список ошибок.</returns>
    public static Structures.ExceptionsStruct ImportBody(Session session, Sungero.Content.IElectronicDocument edoc, string pathToBody, NLog.Logger logger)
    {
      var memoryStream = new MemoryStream();
      var exception = new Structures.ExceptionsStruct();
      try
      {
        // GetExtension возвращает расширение в формате ".<расширение>". Убираем точку.
        var ext = Path.GetExtension(pathToBody).Replace(".", "");
        // TODO Кэшировать.
        var apps = Enumerable.ToList(session.GetAll<Sungero.Content.IAssociatedApplication>().Where(x => x.Extension == ext));
        var app = (Enumerable.FirstOrDefault<Sungero.Content.IAssociatedApplication>(apps));
        if (app != null)
        {
          using (FileStream file = new FileStream(pathToBody, FileMode.Open, FileAccess.Read))
            file.CopyTo(memoryStream);
          edoc.CreateVersion();
          edoc.LastVersion.AssociatedApplication = app;
          edoc.LastVersion.Body.Write(memoryStream);
          edoc.Save();
          session.SubmitChanges();
        }
        else
        {
          exception.ErrorType = "Error";
          exception.Message = string.Format("Не обнаружено соответствующее приложение-обработчик для файлов с расширением \"{0}\"", ext);
          logger.Error(exception.Message);
          return exception;
        }
      }
      catch (Exception ex)
      {
        exception.ErrorType = "Error";
        exception.Message = string.Format("Не удается создать тело документа. Ошибка: \"{0}\"", ex.Message);
        logger.Error(exception.Message);
        return exception;
      }
      return exception;
    }

    /// <summary>
    /// Регистрация документа.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="edoc">Экземпляр документа.</param>
    /// <param name="documentRegisterId">ИД журнала регистрации.</param>
    /// <param name="regNumber">Рег. №</param>
    /// <param name="regDate">Дата регистрации.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Список ошибок.</returns>
    public static IEnumerable<Structures.ExceptionsStruct> RegisterDocument(Session session, Sungero.Docflow.IOfficialDocument edoc, int documentRegisterId, string regNumber, DateTime? regDate, Guid defaultRegistrationRoleGuid, NLog.Logger logger)
    {
      var exceptionList = new List<Structures.ExceptionsStruct>();

      Sungero.Docflow.IRegistrationGroup regGroup = null;
      // TODO Кэшировать.
      var documentRegister = BusinessLogic.GetDocumentRegister(session, documentRegisterId);
      if (documentRegister != null && regDate != null && !string.IsNullOrEmpty(regNumber))
      {
        edoc.RegistrationDate = regDate;
        edoc.RegistrationNumber = regNumber;
        edoc.DocumentRegister = documentRegister;
        edoc.RegistrationState = Sungero.Docflow.OfficialDocument.RegistrationState.Registered;
        regGroup = documentRegister.RegistrationGroup;
      }
      else
      {
        var message = string.Format("Не удалось найти соответствующий реестр с ИД \"{0}\".", documentRegisterId);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }

      if (regGroup != null)
        edoc.AccessRights.Grant(regGroup, DefaultAccessRightsTypes.FullAccess);
      else
      {
        var message = string.Format("Не была найдена соответствующая группа регистрации. Права на документ будут выданы для роли c Guid {0}.", defaultRegistrationRoleGuid.ToString());
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
        var regRole = BusinessLogic.GetRoleBySid(session, defaultRegistrationRoleGuid);
        edoc.AccessRights.Grant(regRole, DefaultAccessRightsTypes.FullAccess);
      }
      try
      {
        edoc.Save();
      }
      catch (Exception ex)
      {
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = ex.Message });
      }
      return exceptionList;
    }

    /// <summary>
    /// Получение ЖЦ документа.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="lifeCycleStateName">Наименование ЖЦ документа.</param>
    /// <returns>ЖЦ.</returns>
    public static Sungero.Core.Enumeration? GetPropertyLifeCycleState(Session session, string lifeCycleStateName)
    {
      Dictionary<string, Sungero.Core.Enumeration?> LifeCycleStates = new Dictionary<string, Sungero.Core.Enumeration?>
      {
        {"В разработке", Sungero.Contracts.Contract.LifeCycleState.Draft},
        {"Действующий", Sungero.Contracts.Contract.LifeCycleState.Active},
        {"Аннулирован", Sungero.Contracts.Contract.LifeCycleState.Obsolete},
        {"Расторгнут", Sungero.Contracts.Contract.LifeCycleState.Terminated},
        {"Исполнен", Sungero.Contracts.Contract.LifeCycleState.Closed},
        {"", null}
      };
      return LifeCycleStates[lifeCycleStateName];
    }

    /// <summary>
    /// Получение пола.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="sexPropertyName">Наименование пола.</param>
    /// <returns>Экземпляр записи "Пол".</returns>
    public static Sungero.Core.Enumeration? GetPropertySex(Session session, string sexPropertyName)
    {
      Dictionary<string, Sungero.Core.Enumeration?> sexProperty = new Dictionary<string, Sungero.Core.Enumeration?>
      {
        {"Мужской", Sungero.Parties.Person.Sex.Male},
        {"Женский", Sungero.Parties.Person.Sex.Female},
        {"Male", Sungero.Parties.Person.Sex.Male},
        {"Female", Sungero.Parties.Person.Sex.Female},
        {"", null}
      };
      return sexProperty[sexPropertyName];
    }

    /// <summary>
    /// Получение типа документа по наименованию.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="documentKindName">Наименование типа документа.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Состояние.</returns>
    public static Sungero.Docflow.IDocumentKind GetDocumentKind(Session session, string documentKindName, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var documentKinds = Enumerable.ToList(session.GetAll<Sungero.Docflow.IDocumentKind>().Where(x => x.Name == documentKindName));
      var documentKind = (Enumerable.FirstOrDefault<Sungero.Docflow.IDocumentKind>(documentKinds));
      if (documentKinds.Count > 1)
      {
        var message = string.Format("Найдено несколько типов документов с именем \"{0}\". Проверьте, что в выбрана верная запись.", documentKind.Name);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return documentKind;
    }

    /// <summary>
    /// Получение журнала регистрации.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="documentRegisterId">ИД журнала регистрации.</param>
    /// <returns>Журнал регистрации.</returns>
    public static Sungero.Docflow.IDocumentRegister GetDocumentRegister(Session session, int documentRegisterId)
    {
      // TODO Кэшировать.
      var documentRegisters = Enumerable.ToList(session.GetAll<Sungero.Docflow.IDocumentRegister>().Where(x => x.Id == documentRegisterId));
      var documentRegister = (Enumerable.FirstOrDefault<Sungero.Docflow.IDocumentRegister>(documentRegisters));
      return documentRegister;
    }
    #endregion
    #region Получение экземпляров сущностей.
    /// <summary>
    /// Получение контрагента по наименованию.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование контрагента.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Контрагент.</returns>
    public static Sungero.Parties.ICounterparty GetConterparty(Session session, string conterpartyName, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var conterparties = Enumerable.ToList(session.GetAll<Sungero.Parties.ICounterparty>().Where(x => x.Name == conterpartyName));
      var conterparty = (Enumerable.FirstOrDefault<Sungero.Parties.ICounterparty>(conterparties));
      if (conterparties.Count > 1)
      {
        var message = string.Format("Найдено несколько контрагентов с именем \"{0}\". Проверьте, что в выбрана верная запись.", conterpartyName);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return conterparty;
    }

    /// <summary>
    /// Создание организации.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование организации.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Организация.</returns>
    public static Sungero.Parties.ICompany CreateConterparty(Session session, string companyName, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var companies = Enumerable.ToList(session.GetAll<Sungero.Parties.ICompany>().Where(x => x.Name == companyName));
      var company = (Enumerable.FirstOrDefault<Sungero.Parties.ICompany>(companies));
      if (company == null)
        try
        {
          company = session.Create<Sungero.Parties.ICompany>();
          company.Name = companyName;
          company.Save();
        }
        catch (Exception ex)
        {
          var message = string.Format("Не удалось создать организацию \"{0}\". Текст ошибки: {1}.", company.Name, ex.Message);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }
      return company;
    }

    /// <summary>
    /// Получение категории договора.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование категории договора.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Категория договора.</returns>
    public static Sungero.Contracts.IContractCategory GetContractCategory(Session session, string contractCategoryName, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var contractCategories = Enumerable.ToList(session.GetAll<Sungero.Contracts.IContractCategory>().Where(x => x.Name == contractCategoryName));
      var contractCategory = (Enumerable.FirstOrDefault<Sungero.Contracts.IContractCategory>(contractCategories));
      if (contractCategories.Count > 1)
      {
        var message = string.Format("Найдено несколько категорий договоров с именем \"{0}\". Проверьте, что в выбрана верная запись.", contractCategory.Name);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return contractCategory;
    }

    /// <summary>
    /// Получение НОР.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование НОР.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>НОР.</returns>
    public static Sungero.Company.IBusinessUnit GetBusinessUnit(Session session, string businessUnitName, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var businessUnits = Enumerable.ToList(session.GetAll<Sungero.Company.IBusinessUnit>().Where(x => x.Name == businessUnitName));
      var businessUnit = (Enumerable.FirstOrDefault<Sungero.Company.IBusinessUnit>(businessUnits));
      if (businessUnits.Count > 1)
      {
        var message = string.Format("Найдено несколько наших организаций именем \"{0}\". Проверьте, что в выбрана верная запись.", businessUnit.Name);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return businessUnit;
    }

    /// <summary>
    /// Создание НОР.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование НОР.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>НОР.</returns>
    public static Sungero.Company.IBusinessUnit CreateBusinessUnit(Session session, string businessUnitName, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var businessUnits = Enumerable.ToList(session.GetAll<Sungero.Company.IBusinessUnit>().Where(x => x.Name == businessUnitName));
      var businessUnit = (Enumerable.FirstOrDefault<Sungero.Company.IBusinessUnit>(businessUnits));
      if (businessUnit == null)
        try
        {
          businessUnit = session.Create<Sungero.Company.IBusinessUnit>();
          businessUnit.Name = businessUnitName;
          businessUnit.Save();
        }
        catch (Exception ex)
        {
          var message = string.Format("Не удалось создать НОР \"{0}\". Текст ошибки: {1}.", businessUnit.Name, ex.Message);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }
      return businessUnit;
    }

    /// <summary>
    /// Получение подразделения.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование подразделения.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Подразделение.</returns>
    public static Sungero.Company.IDepartment GetDepartment(Session session, string departmentName, int? businessUnitId, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var departments = Enumerable.ToList(session.GetAll<Sungero.Company.IDepartment>().Where(x => x.Name == departmentName && businessUnitId.HasValue && x.BusinessUnit.Id == businessUnitId));
      var department = (Enumerable.FirstOrDefault<Sungero.Company.IDepartment>(departments));
      if (departments.Count > 1)
      {
        var message = string.Format("Найдено несколько подразделений именем \"{0}\". Проверьте, что в выбрана верная запись.", department.Name);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return department;
    }

    /// <summary>
    /// Создание подразделения.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование подразделения.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Подразделение.</returns>
    public static Sungero.Company.IDepartment CreateDepartment(Session session, string name, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var departments = Enumerable.ToList(session.GetAll<Sungero.Company.IDepartment>().Where(x => x.Name == name));
      var department = (Enumerable.FirstOrDefault<Sungero.Company.IDepartment>(departments));
      if (department == null)
        try
        {
          department = session.Create<Sungero.Company.IDepartment>();
          department.Name = name;
          department.Save();
        }
        catch (Exception ex)
        {
          var message = string.Format("Не удалось создать подразделение \"{0}\". Текст ошибки: {1}.", department.Name, ex.Message);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }
      return department;
    }

    /// <summary>
    /// Получение валюты.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование валюты.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Валюта.</returns>
    public static Sungero.Commons.ICurrency GetCurrency(Session session, string name, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var currencies = Enumerable.ToList(session.GetAll<Sungero.Commons.ICurrency>().Where(x => x.Name == name));
      var currency = (Enumerable.FirstOrDefault<Sungero.Commons.ICurrency>(currencies));
      if (currencies.Count > 1)
      {
        var message = string.Format("Найдено несколько валют именем \"{0}\". Проверьте, что в выбрана верная запись.", currency.Name);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return currency;
    }

    /// <summary>
    /// Получение сотрудника.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование сотрудника.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Сотрудник.</returns>
    public static Sungero.Company.IEmployee GetEmployee(Session session, string employeeName, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var employees = Enumerable.ToList(session.GetAll<Sungero.Company.IEmployee>().Where(x => x.Name == employeeName));
      var employee = (Enumerable.FirstOrDefault<Sungero.Company.IEmployee>(employees));
      if (employees.Count > 1)
      {
        var message = string.Format("Найдено несколько сотрудников с именем \"{0}\". Проверьте, что в выбрана верная запись.", employee.Name);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return employee;
    }

    /// <summary>
    /// Получение роли по Sid.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="sid">Sid.</param>
    /// <returns>Роль.</returns>
    public static Sungero.CoreEntities.IRole GetRoleBySid(Session session, Guid sid)
    {
      // TODO Кэшировать.
      var roles = Enumerable.ToList(session.GetAll<Sungero.CoreEntities.IRole>().Where(x => x.Sid == sid));
      var role = (Enumerable.FirstOrDefault<Sungero.CoreEntities.IRole>(roles));
      return role;
    }

    /// <summary>
    /// Получение Города.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование города.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Город.</returns>
    public static Sungero.Commons.ICity GetCity(Session session, string name, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var cities = Enumerable.ToList(session.GetAll<Sungero.Commons.ICity>().Where(x => x.Name == name));
      var city = (Enumerable.FirstOrDefault<Sungero.Commons.ICity>(cities));
      if (cities.Count > 1)
      {
        var message = string.Format("Найдено несколько городов с наименованием \"{0}\". Проверьте, что в выбрана верная запись.", city.Name);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return city;
    }

    /// <summary>
    /// Получение Региона.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование города.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Регион.</returns>
    public static Sungero.Commons.IRegion GetRegion(Session session, string name, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var regions = Enumerable.ToList(session.GetAll<Sungero.Commons.IRegion>().Where(x => x.Name == name));
      var region = (Enumerable.FirstOrDefault<Sungero.Commons.IRegion>(regions));
      if (regions.Count > 1)
      {
        var message = string.Format("Найдено несколько регионов с наименованием \"{0}\". Проверьте, что в выбрана верная запись.", region.Name);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return region;
    }

    /// <summary>
    /// Получение Банка.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование банка.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Банк.</returns>
    public static Sungero.Parties.IBank GetBank(Session session, string name, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var banks = Enumerable.ToList(session.GetAll<Sungero.Parties.IBank>().Where(x => x.Name == name));
      var bank = (Enumerable.FirstOrDefault<Sungero.Parties.IBank>(banks));
      if (banks.Count > 1)
      {
        var message = string.Format("Найдено несколько банков с наименованием \"{0}\". Проверьте, что в выбрана верная запись.", bank.Name);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return bank;
    }

    /// <summary>
    /// Получение Персоны.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="lastName">Фамилия.</param>
    /// <param name="firstName">Имя.</param>
    /// <param name="middleName">Отчество.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Персона.</returns>
    public static Sungero.Parties.IPerson GetPerson(Session session, string lastName, string firstName, string middleName, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var persons = Enumerable.ToList(session.GetAll<Sungero.Parties.IPerson>().Where(x => x.LastName == lastName && x.FirstName == firstName && x.MiddleName == middleName && !string.IsNullOrWhiteSpace(middleName) ||
          x.LastName == lastName && x.FirstName == firstName && string.IsNullOrWhiteSpace(middleName)));
      var person = (Enumerable.FirstOrDefault<Sungero.Parties.IPerson>(persons));
      if (persons.Count > 1)
      {
        var message = string.Format("Найдено несколько персон с ФИО \"{0} {1} {2}\". Проверьте, что в выбрана верная запись.", lastName, firstName, middleName);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return person;
    }

    /// <summary>
    /// Получение должности.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование должности.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Должность.</returns>
    public static Sungero.Company.IJobTitle GetJobTitle(Session session, string name, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var jobTitles = Enumerable.ToList(session.GetAll<Sungero.Company.IJobTitle>().Where(x => x.Name == name));
      var jobTitle = (Enumerable.FirstOrDefault<Sungero.Company.IJobTitle>(jobTitles));
      if (jobTitles.Count > 1)
      {
        var message = string.Format("Найдено несколько должностей с именем \"{0}\". Проверьте, что в выбрана верная запись.", jobTitle.Name);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return jobTitle;
    }

    /// <summary>
    /// Создание должности.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="companyName">Наименование должности.</param>
    /// <param name="exceptionList">Список ошибок.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Должность.</returns>
    public static Sungero.Company.IJobTitle CreateJobTitle(Session session, string name, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var jobTitles = Enumerable.ToList(session.GetAll<Sungero.Company.IJobTitle>().Where(x => x.Name == name));
      var jobTitle = (Enumerable.FirstOrDefault<Sungero.Company.IJobTitle>(jobTitles));
      if (jobTitle == null)
        try
        {
          jobTitle = session.Create<Sungero.Company.IJobTitle>();
          jobTitle.Name = name;
          jobTitle.Save();
        }
        catch (Exception ex)
        {
          var message = string.Format("Не удалось создать должность \"{0}\". Текст ошибки: {1}.", jobTitle.Name, ex.Message);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }
      return jobTitle;
    }

    /// <summary>
    /// Получение способа доставки.
    /// </summary>
    /// <param name="session">Текущая сессия.</param>
    /// <param name="deliveryMethod">Способ доставки.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Способ доставки.</returns>
    public static Sungero.Docflow.IMailDeliveryMethod GetMailDeliveryMethod(Session session, string deliveryMethod, List<Structures.ExceptionsStruct> exceptionList, NLog.Logger logger)
    {
      // TODO Кэшировать.
      var mailDeliveryMethods = Enumerable.ToList(session.GetAll<Sungero.Docflow.IMailDeliveryMethod>().Where(x => x.Name == deliveryMethod));
      var mailDeliveryMethod = (Enumerable.FirstOrDefault<Sungero.Docflow.IMailDeliveryMethod>(mailDeliveryMethods));
      if (mailDeliveryMethods.Count > 1)
      {
        var message = string.Format("Найдено несколько способов доставки с наименованием \"{0}\". Проверьте, что в выбрана верная запись.", deliveryMethod);
        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
        logger.Warn(message);
      }
      return mailDeliveryMethod;
    }
    #endregion
    #region Проверка валидации.
    /// <summary>
    /// Проверка введенного ОГРН по количеству символов.
    /// </summary>
    /// <param name="psrn">ОГРН.</param>
    /// <returns>Пустая строка, если длина ОГРН в порядке.
    /// Иначе текст ошибки.</returns>
    public static string CheckPsrnLength(string psrn)
    {
      if (string.IsNullOrWhiteSpace(psrn))
        return string.Empty;

      psrn = psrn.Trim();

      return System.Text.RegularExpressions.Regex.IsMatch(psrn, @"(^\d{13}$)|(^\d{15}$)") ? string.Empty : Constants.Resources.IncorrecPsrnLength;
    }

    /// <summary>
    /// Проверка введенного КПП по количеству символов.
    /// </summary>
    /// <param name="trrc">КПП.</param>
    /// <returns>Пустая строка, если длина КПП в порядке.
    /// Иначе текст ошибки.</returns>
    public static string CheckTrrcLength(string trrc)
    {
      if (string.IsNullOrWhiteSpace(trrc))
        return string.Empty;

      trrc = trrc.Trim();

      return System.Text.RegularExpressions.Regex.IsMatch(trrc, @"(^\d{9}$)") ? string.Empty : Constants.Resources.IncorrecTrrcLength;
    }

    /// <summary>
    /// Проверка введенного кода подразделения по количеству символов.
    /// </summary>
    /// <param name="codeDepartment">Код подразделения.</param>
    /// <returns>Пустая строка, если длина кода подразделения в порядке.
    /// Иначе текст ошибки.</returns>
    public static string CheckCodeDepartmentLength(string codeDepartment)
    {
      if (string.IsNullOrWhiteSpace(codeDepartment))
        return string.Empty;

      codeDepartment = codeDepartment.Trim();

      return codeDepartment.Length <= 10 ? string.Empty : Constants.Resources.IncorrecCodeDepartmentLength;
    }
    #endregion
  }
}
