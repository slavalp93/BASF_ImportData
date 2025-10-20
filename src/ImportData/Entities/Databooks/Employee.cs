using System;
using System.Collections.Generic;
using NLog;
using System.Linq;
using Sungero.Domain.Client;
using Sungero.Domain.ClientLinqExpressions;

namespace ImportData
{
  class Employee : Person
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

      exceptionList.AddRange(base.SaveToRX(logger, supplementEntity, ignoreDuplicates, 2));

      using (var session = new Session())
      {
        var lastName = this.Parameters[shift + 2].Trim();
        if (string.IsNullOrEmpty(lastName))
        {
          var message = string.Format("Не заполнено поле \"Фамилия\".");
          exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = Constants.ErrorTypes.Error, Message = message});
          logger.Error(message);
          return exceptionList;
        }
        var firstName = this.Parameters[shift + 3].Trim();
        if (string.IsNullOrEmpty(firstName))
        {
          var message = string.Format("Не заполнено поле \"Имя\".");
          exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = Constants.ErrorTypes.Error, Message = message});
          logger.Error(message);
          return exceptionList;
        }
        var middleName = this.Parameters[shift + 4].Trim();
        var person = BusinessLogic.GetPerson(session, lastName, firstName, middleName, exceptionList, logger);
        if (person == null)
        {
          var message = string.Format("Не удалось создать персону \"{0} {1} {2}\".", lastName, firstName, middleName);
          exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = Constants.ErrorTypes.Error, Message = message});
          logger.Error(message);
          return exceptionList;
        }
        var name = person.Name;
        var department = BusinessLogic.GetDepartment(session, this.Parameters[shift + 0].Trim(), null, exceptionList, logger);
        if (department == null && !string.IsNullOrEmpty(this.Parameters[shift + 0].Trim()))
        {
          department = BusinessLogic.CreateDepartment(session, this.Parameters[shift + 0].Trim(), exceptionList, logger);
          //var message = string.Format("Не на найдено подразделение \"{0}\".", this.Parameters[shift + 0].Trim());
          //exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = Constants.ErrorTypes.Error, Message = message});
          //logger.Error(message);
          //return exceptionList;
        }
        var jobTitle = BusinessLogic.GetJobTitle(session, this.Parameters[shift + 1].Trim(), exceptionList, logger);
        if (jobTitle == null && !string.IsNullOrEmpty(this.Parameters[shift + 1].Trim()))
        {
          jobTitle = BusinessLogic.CreateJobTitle(session, this.Parameters[shift + 1].Trim(), exceptionList, logger);
        }
        var email = this.Parameters[shift + 14].Trim();
        var phone = this.Parameters[shift + 13].Trim();
        var note = this.Parameters[shift + 18].Trim();

        try
        {
          var employee = Sungero.Company.Employees.Null;
          if (ignoreDuplicates.ToLower() != Constants.ignoreDuplicates.ToLower())
          {
            var employees = Enumerable.ToList(session.GetAll<Sungero.Company.IEmployee>().Where(x => x.Name == name));
            employee = (Enumerable.FirstOrDefault<Sungero.Company.IEmployee>(employees));
            if (employee != null)
            {
              var message = string.Format("Сотрудник не может быть импортирован. Найден дубль \"{0}\".", name);
              exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
              logger.Error(message);
              return exceptionList;
            }
          }
          employee = session.Create<Sungero.Company.IEmployee>();
          employee.Name = name;
          employee.Person = person;
          employee.Department = department;
          employee.JobTitle = jobTitle;
          employee.Email = email;
          employee.Phone = phone;
          employee.Note = note;
          employee.NeedNotifyExpiredAssignments = false;
          employee.NeedNotifyNewAssignments = false;
          employee.Save();

        }
        catch (Exception ex)
        {
          exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = "Error", Message = ex.Message});
          return exceptionList;
        }
        session.SubmitChanges();
      }
      return exceptionList;
    }
  }
}
