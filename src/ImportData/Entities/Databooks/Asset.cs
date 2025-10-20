using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using NLog;
using Sungero.Domain.Client;
using Sungero.Domain.ClientLinqExpressions;

namespace ImportData
{
    class Asset : Entity
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
                // Asset (Номер в SAP)
                var codeSAP = this.Parameters[shift + 0].Trim();
                if (string.IsNullOrEmpty(codeSAP))
                {
                    var message = string.Format("Не заполнено поле \"Asset\".");
                    exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = "Error", Message = message });
                    logger.Error(message);
                    return exceptionList;
                }

                // Asset description (Название основного средства)
                var name = this.Parameters[shift + 1].Trim();
                if (string.IsNullOrEmpty(name))
                {
                    var message = string.Format("Не заполнено поле \"Asset description\".");
                    exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = "Error", Message = message });
                    logger.Error(message);
                    return exceptionList;
                }

                // Serial number
                var serialNumber = this.Parameters[shift + 2].Trim();
                if (string.IsNullOrEmpty(serialNumber))
                {
                    var message = string.Format("Не заполнено поле \"Serial number\".");
                    exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = "Error", Message = message });
                    logger.Error(message);
                    return exceptionList;
                }

                // Inventory number
                var inventoryNumber = this.Parameters[shift + 3].Trim();
                if (string.IsNullOrEmpty(inventoryNumber))
                {
                    var message = string.Format("Не заполнено поле \"Inventory number\".");
                    exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = "Error", Message = message });
                    logger.Error(message);
                    return exceptionList;
                }

                // Acquis.val.
                var style = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;
                var culture = CultureInfo.CreateSpecificCulture("en-GB");

                var acquisVal = this.Parameters[shift + 4].Trim();
                double initialCost = 0.0;
                if (!string.IsNullOrWhiteSpace(acquisVal)) 
                    double.TryParse(acquisVal, style, culture, out initialCost);                

                // Useful life
                var usefulLife = this.Parameters[shift + 5].Trim();
                int usefulLifeInt = 0;
                if (!string.IsNullOrWhiteSpace(usefulLife))
                    int.TryParse(usefulLife, out usefulLifeInt);
                    
                // Capitalized on
                var capitalizedOn = this.Parameters[shift + 6].Trim();
                DateTime commissioningDate = this.ParseDate(capitalizedOn, NumberStyles.None, CultureInfo.InvariantCulture);

                // BusinessUnit
                var businessUnitName = this.Parameters[shift + 7].Trim();
                if (string.IsNullOrEmpty(businessUnitName))
                {
                    var message = string.Format("Не заполнено поле \"BusinessUnit\".");
                    exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = "Error", Message = message });
                    logger.Error(message);
                    return exceptionList;
                }

                // Responsible
                var responsibleName = this.Parameters[shift + 8].Trim();

                try
                {
                    var asset = litiko.Assets.Assets.Null;
                    if (ignoreDuplicates.ToLower() != Constants.ignoreDuplicates.ToLower())
                    {
                        var assets = Enumerable.ToList(session.GetAll<litiko.Assets.IAsset>().Where(x => x.SAPNumber == codeSAP));
                        asset = (Enumerable.FirstOrDefault<litiko.Assets.IAsset>(assets));
                        /*
                        if (asset != null)
                        {
                            if (!supplementEntity)
                            {
                                var message = string.Format("Запись не может быть импортирована. Найден дубль по реквизитам Номер SAP: \"{0}\"", codeSAP);
                                exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
                                logger.Error(message);
                                return exceptionList;
                            }
                        }
                        */
                    }
                    if (asset == null)
                        asset = session.Create<litiko.Assets.IAsset>();

                    if (asset.SAPNumber != codeSAP)
                        asset.SAPNumber = codeSAP;

                    if (asset.Name != name)
                        asset.Name = name;
                    
                    if (asset.SerialNumber != serialNumber)
                        asset.SerialNumber = serialNumber;
                    
                    if (asset.InventoryNumber != inventoryNumber)
                        asset.InventoryNumber = inventoryNumber;
                    
                    if (asset.InitialCost != initialCost)
                        asset.InitialCost = initialCost;

                    if (asset.UsefulLife != usefulLifeInt)
                        asset.UsefulLife = usefulLifeInt;

                    if (commissioningDate != DateTime.MinValue && asset.CommissioningDate != commissioningDate)
                        asset.CommissioningDate = commissioningDate;

                    var businessUnit = Sungero.Company.BusinessUnits.Null;
                    businessUnit = (Enumerable.FirstOrDefault<litiko.BASF.IBusinessUnit>(Enumerable.ToList(session.GetAll<litiko.BASF.IBusinessUnit>().Where(x => x.Name == businessUnitName || x.EGRPOUlitiko == businessUnitName))));
                    if (businessUnit == null)
                    {
                        var message = string.Format("Не найдено Нашу организацию по имени {0}", businessUnitName);
                        exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
                        logger.Error(message);
                        //return exceptionList;
                    }
                    else
                    {
                        if (asset.BusinessUnit?.Id != businessUnit.Id)
                            asset.BusinessUnit = businessUnit;
                    }

                    var responsible = litiko.BASF.Employees.Null;
                    if (businessUnit != null && !string.IsNullOrWhiteSpace(responsibleName))
                        responsible = (Enumerable.FirstOrDefault<litiko.BASF.IEmployee>(Enumerable.ToList(session.GetAll<litiko.BASF.IEmployee>().Where(x => x.Name == responsibleName && x.Department.BusinessUnit.Id == businessUnit.Id))));
                    if (responsible != null && asset.Responsible?.Id != responsible.Id)
                        asset.Responsible = responsible;

                    if (asset.State.IsChanged || asset.State.IsInserted)
                        asset.Save();
                }
                catch (Exception ex)
                {
                    var message = string.Format("Ошибка при изменении свойств: \"{0}\"", ex.StackTrace);
                    logger.Error(message);

                    exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = ex.Message });
                    return exceptionList;
                }
                session.SubmitChanges();
            }
            return exceptionList;
        }
    }
}
