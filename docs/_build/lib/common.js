// Общие элементы документов проекта
const { Doc, AlignmentType } = require('./gost');
const T = Doc.tp;

// Титульный лист учебного документа
function academicTitle(docTitle, subtitle) {
  return [
    T('[Наименование образовательной организации]', { size: 26, after: 120 }),
    T('', { before: 2800 }),
    T('ИНФОРМАЦИОННАЯ СИСТЕМА УПРАВЛЕНИЯ ЗАЯВКАМИ', { bold: true, size: 30 }),
    T('В СЕРВИСНЫЙ ЦЕНТР «СЕРВИСДЕСК»', { bold: true, size: 30, after: 500 }),
    T(docTitle.toUpperCase(), { bold: true, size: 36, after: 160 }),
    ...(subtitle ? [T(subtitle, { size: 28 })] : []),
    T('', { before: 2600 }),
    T('Выполнил: студент группы 23П-1', { align: AlignmentType.RIGHT }),
    T('Баймуратов Данил Азатович', { align: AlignmentType.RIGHT, after: 200 }),
    T('Проверил: ______________________', { align: AlignmentType.RIGHT, after: 2200 }),
    T('2026'),
  ];
}

module.exports = { academicTitle };
