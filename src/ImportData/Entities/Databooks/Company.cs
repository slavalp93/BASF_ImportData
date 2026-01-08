using System;
using System.Collections.Generic;
using NLog;
using System.Linq;
using Sungero.Domain.Client;
using Sungero.Domain.ClientLinqExpressions;
using System.Text.RegularExpressions;
using Sungero.Company.PublicFunctions;

namespace ImportData
{
    class Company : Entity
    {
        public int PropertiesCount = 13;
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
                var edrpou = this.Parameters[shift + 0].Trim();
                var name = this.Parameters[shift + 1].Trim();
                var nameEN = this.Parameters[shift + 2].Trim();

                var phones = this.Parameters[shift + 3].Trim();
                var contactFIO = this.Parameters[shift + 4].Trim();
                var contactJobTittle = this.Parameters[shift + 5].Trim();                
                var inn = this.Parameters[shift + 6].Trim();
                var email = this.Parameters[shift + 7].Trim();
                var legalAdress = this.Parameters[shift + 8].Trim();
                var bankName = this.Parameters[shift + 9].Trim();
                //var bankMFO = this.Parameters[shift + 10].Trim();
                var iban = this.Parameters[shift + 11].Trim();
                var certificateVAT = this.Parameters[shift + 12].Trim();

                if (string.IsNullOrEmpty(edrpou))
                {
                    var message = string.Format("Не заповнено поле \"ЄДРПОУ\".");
                    exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = "Error", Message = message });
                    logger.Error(message);
                    return exceptionList;
                }

                if (string.IsNullOrEmpty(name))
                {
                    var message = string.Format("Не заповнено поле \"Повне найменування ЮО/ ПІБ ФОП\".");
                    exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = "Error", Message = message });
                    logger.Error(message);
                    return exceptionList;
                }

                if (string.IsNullOrEmpty(nameEN))
                {
                    var message = string.Format("Не заповнено поле \"Повне найменування ЮО/ ПІБ ФОП (English)\".");
                    exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = "Error", Message = message });
                    logger.Error(message);
                    return exceptionList;
                }

                var company = BusinessLogic.GetCompanyByEDRPOU(session, edrpou, exceptionList, logger);
                if (company == null)
                {
                    company = BusinessLogic.CreateCompany(session, edrpou, name, nameEN, exceptionList, logger);
                }

                try
                {
                    if (!string.IsNullOrEmpty(phones) && company.Phones != phones)
                        company.Phones = phones;

                    if (!string.IsNullOrEmpty(inn) && company.TINlitiko != inn)
                        company.TINlitiko = inn;

                    if (!string.IsNullOrEmpty(email) && company.Email != email)
                        company.Email = email;

                    if (!string.IsNullOrEmpty(legalAdress))
                    {
                        if (company.LegalAddress != legalAdress)
                            company.LegalAddress = legalAdress;
                        if (company.PostalAddress != legalAdress)
                            company.PostalAddress = legalAdress;
                    }
                    
                    if (!string.IsNullOrEmpty(iban) && company.Account != iban)
                        company.Account = iban;

                    if (!string.IsNullOrEmpty(certificateVAT) && company.VATCertlitiko != certificateVAT)
                        company.VATCertlitiko = certificateVAT;

                    if (!string.IsNullOrEmpty(bankName))
                    {
                        var bank = BusinessLogic.GetBank(session, bankName, exceptionList, logger);
                        if (bank == null)
                        {
                            var message = string.Format("Банк не знайдено {0}", bankName);
                            exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
                            logger.Error(message);
                        }
                        else
                        {
                            if (company.Bank?.Id != bank.Id)
                                company.Bank = litiko.BASF.Banks.As(bank);
                        }
                    }

                    if (!string.IsNullOrEmpty(contactFIO))
                    {
                        var contact = BusinessLogic.GetContact(session, company, contactFIO, exceptionList, logger);
                        if (contact == null)                        
                            contact = BusinessLogic.CreateContact(session, company, contactFIO, exceptionList, logger);

                        if (!string.IsNullOrEmpty(contactJobTittle) && contact.JobTitle != contactJobTittle)
                        {
                            contact.JobTitle = contactJobTittle;
                            contact.Save();
                        }
                    }

                    if (company.State.IsChanged || company.State.IsInserted)
                        company.Save();
                }
                catch (Exception ex)
                {
                    var message = string.Format("Помилка при зміні властивостей: \"{0}\"", ex.StackTrace);
                    logger.Error(message);

                    Console.WriteLine(ex.Message);
                    exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = ex.Message });
                    return exceptionList;
                }
                session.SubmitChanges();
            }
            return exceptionList;
        }
    }
}
