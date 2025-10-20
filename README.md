# rx-util-importdata
Репозиторий с шаблоном разработки «Утилита импорта данных» для версий Directum RX до 3.6 включительно.
Для Directum RX 4.0 и выше необходимо использовать новую версию утилиты: https://github.com/DirectumCompany/rx-util-importdata-net-core

## Описание
Шаблон позволяет перенести исторические данные в Directum RX. 
В качестве источника данных для переноса используются книги Excel с расширением XLSX.
Чтобы произвести миграцию документов и справочников в Directum RX из заменяемой системы, достаточно заполнить специально сформированные шаблоны Excel и запустить утилиту из командной строки с необходимыми параметрами.

### На текущий момент реализована возможность импорта следующих типов документов:
1. Договоры.
2. Дополнительные соглашения.
3. Входящие письма.
4. Исходящие письма.
5. Приказы.

### Справочников:
1. Организации (Контрагенты).
2. Наши организации.
3. Подразделения.
4. Должности.
5. Сотрудники.
6. Персоны.

## Модификация

Для модификации утилиты требуется наличие на рабочем месте разработчика:
1. Visual Studio 2017 и выше.
2. Установленый пакет .net framework 4.6.2 и выше.

Модификация выполняется за счет наследования и доработки реализованных классов:
* класс Entity - базовый абстрактный класс, от которого наследованы остальные классы;
* классы справочников (Databooks):  BusinessUnit, Company, Department, Employee, Person - реализуют процесс импорта исторических данных в справочники системы Directum RX;
* классы документов (EDocs): Contract, IncomingLetter, Order, OutgoingLetter, SupAgreement - реализуют процесс импорта документов с телами (или без тел) в систему Directum RX;
* методы, которые прямо не относятся к классам сущностей реализуются в классе BusinessLogic;
* механизмы работы с XLSX реализованы в классе ExcelProcessor. В качестве механизма работы с XLSX используется библиотека OpenXml.
* общие механизмы работы с сущностями реализованы в классах EntityProcessor и EntityWrapper.

## Подготовка инсталлятора

Для сборки инсталлятора используется утилита NSIS (https://github.com/kichik/nsis).
В папке решения https://github.com/DirectumCompany/rx-util-importdata/tree/master/Install находится готовый конфигурационный файл DirRxInstaller.nsi для генерации инсталлятора.

## Порядок установки и использования

Порядок установки и использования описан в документе https://github.com/DirectumCompany/rx-util-importdata/blob/master/doc/Instructions_for_loading_data_into_DirectumRX_rus.pdf

## Порядок кастомизации утилиты (для партнеров)
Для работы требуется установленный Directum RX версии 3.3 и выше.

Установка для ознакомления

1. Склонировать репозиторий https://github.com/DirectumCompany/rx-util-importdata.git в папку.
2. Указать в _ConfigSettings.xml DDS:
```xml 
<block name="REPOSITORIES">
  <repository folderName="Base" solutionType="Base" url="" /> 
  <repository folderName="<Папка из п.1>" solutionType="Work" 
     url="https://github.com/DirectumCompany/rx-util-importdata.git" />
</block>
```

### Установка для использования на проекте

Возможные варианты

A. Fork репозитория.
1. Сделать fork репозитория <Название репозитория> для своей учетной записи.
2. Склонировать созданный в п. 1 репозиторий в папку.
3. Указать в _ConfigSettings.xml DDS:
```xml 
<block name="REPOSITORIES">
  <repository folderName="Base" solutionType="Base" url="" /> 
  <repository folderName="<Папка из п.2>" solutionType="Work" 
     url="https://github.com/DirectumCompany/rx-util-importdata.git" />
</block>
```

B. Подключение на базовый слой.

Вариант не рекомендуется, так как при выходе версии шаблона разработки не гарантируется обратная совместимость.
1. Склонировать репозиторий <Название репозитория> в папку.
2. Указать в _ConfigSettings.xml DDS:
```xml
<block name="REPOSITORIES">
  <repository folderName="Base" solutionType="Base" url="" /> 
  <repository folderName="<Папка из п.1>" solutionType="Base" 
     url="https://github.com/DirectumCompany/rx-util-importdata.git" />
  <repository folderName="<Папка для рабочего слоя>" solutionType="Work" 
     url="<Адрес репозитория для рабочего слоя>" />
</block>
```

C. Копирование репозитория в систему контроля версий.

Рекомендуемый вариант для проектов внедрения.
1. В системе контроля версий с поддержкой git создать новый репозиторий.
2. Склонировать репозиторий https://github.com/DirectumCompany/rx-util-importdata.git в папку с ключом --mirror.
3. Перейти в папку из п. 2.
4. Импортировать клонированный репозиторий в систему контроля версий командой:
git push –mirror <Адрес репозитория из п. 1>
