-- =====================================================================
--  ИС «СервисДеск» — управление заявками в сервисный центр
--  Схема базы данных (MySQL 8.0, InnoDB, utf8mb4)
--  Автор: Баймуратов Д.А., группа 23П-1
-- =====================================================================

CREATE DATABASE IF NOT EXISTS servicedesk
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE servicedesk;

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

DROP VIEW  IF EXISTS v_orders_overview;
DROP VIEW  IF EXISTS v_parts_stock;
DROP TABLE IF EXISTS documents;
DROP TABLE IF EXISTS payments;
DROP TABLE IF EXISTS inventory_items;
DROP TABLE IF EXISTS inventories;
DROP TABLE IF EXISTS part_movements;
DROP TABLE IF EXISTS part_reservations;
DROP TABLE IF EXISTS parts;
DROP TABLE IF EXISTS order_works;
DROP TABLE IF EXISTS services;
DROP TABLE IF EXISTS order_status_history;
DROP TABLE IF EXISTS order_completeness;
DROP TABLE IF EXISTS completeness_items;
DROP TABLE IF EXISTS orders;
DROP TABLE IF EXISTS order_statuses;
DROP TABLE IF EXISTS devices;
DROP TABLE IF EXISTS device_types;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS roles;

SET FOREIGN_KEY_CHECKS = 1;

-- ---------------------------------------------------------------------
--  Учётные записи и роли
-- ---------------------------------------------------------------------

CREATE TABLE roles (
    id    TINYINT      NOT NULL COMMENT 'Идентификатор роли',
    code  VARCHAR(20)  NOT NULL COMMENT 'Системный код роли (client, receptionist, master, storekeeper)',
    name  VARCHAR(50)  NOT NULL COMMENT 'Наименование роли',
    CONSTRAINT pk_roles PRIMARY KEY (id),
    CONSTRAINT uq_roles_code UNIQUE (code)
) ENGINE = InnoDB COMMENT = 'Роли пользователей';

CREATE TABLE users (
    id                  INT           NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор пользователя',
    role_id             TINYINT       NOT NULL COMMENT 'Роль пользователя',
    last_name           VARCHAR(60)   NOT NULL COMMENT 'Фамилия',
    first_name          VARCHAR(60)   NOT NULL COMMENT 'Имя',
    middle_name         VARCHAR(60)   NULL     COMMENT 'Отчество',
    phone               VARCHAR(16)   NOT NULL COMMENT 'Телефон в формате +7XXXXXXXXXX, используется как логин',
    email               VARCHAR(120)  NULL     COMMENT 'Электронная почта, альтернативный логин',
    password_hash       VARCHAR(100)  NULL     COMMENT 'Хеш пароля BCrypt; NULL — клиент заведён приёмщиком и ещё не зарегистрировался',
    is_active           TINYINT(1)    NOT NULL DEFAULT 1 COMMENT 'Учётная запись активна',
    failed_login_count  TINYINT       NOT NULL DEFAULT 0 COMMENT 'Число неудачных попыток входа подряд',
    locked_until        DATETIME      NULL     COMMENT 'Блокировка входа до указанного времени (после 5 неудачных попыток)',
    created_at          DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Дата регистрации',
    CONSTRAINT pk_users PRIMARY KEY (id),
    CONSTRAINT uq_users_phone UNIQUE (phone),
    CONSTRAINT uq_users_email UNIQUE (email),
    CONSTRAINT fk_users_role FOREIGN KEY (role_id) REFERENCES roles (id),
    CONSTRAINT ck_users_failed CHECK (failed_login_count >= 0)
) ENGINE = InnoDB COMMENT = 'Пользователи системы (клиенты и сотрудники)';

-- ---------------------------------------------------------------------
--  Подсистема «Приём заявок»
-- ---------------------------------------------------------------------

CREATE TABLE device_types (
    id    INT          NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор типа',
    name  VARCHAR(60)  NOT NULL COMMENT 'Наименование типа устройства',
    CONSTRAINT pk_device_types PRIMARY KEY (id),
    CONSTRAINT uq_device_types_name UNIQUE (name)
) ENGINE = InnoDB COMMENT = 'Типы устройств';

CREATE TABLE devices (
    id              INT           NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор устройства',
    client_id       INT           NOT NULL COMMENT 'Владелец устройства (клиент)',
    device_type_id  INT           NOT NULL COMMENT 'Тип устройства',
    brand           VARCHAR(60)   NOT NULL COMMENT 'Производитель',
    model           VARCHAR(100)  NOT NULL COMMENT 'Модель',
    serial_number   VARCHAR(60)   NULL     COMMENT 'Серийный номер / IMEI',
    created_at      DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Дата регистрации устройства',
    CONSTRAINT pk_devices PRIMARY KEY (id),
    CONSTRAINT fk_devices_client FOREIGN KEY (client_id) REFERENCES users (id),
    CONSTRAINT fk_devices_type FOREIGN KEY (device_type_id) REFERENCES device_types (id),
    INDEX ix_devices_serial (serial_number)
) ENGINE = InnoDB COMMENT = 'Устройства клиентов';

CREATE TABLE order_statuses (
    id          TINYINT      NOT NULL COMMENT 'Идентификатор статуса',
    code        VARCHAR(20)  NOT NULL COMMENT 'Системный код статуса',
    name        VARCHAR(50)  NOT NULL COMMENT 'Наименование статуса',
    color       CHAR(7)      NOT NULL COMMENT 'Цвет отображения (HEX) по руководству по стилю',
    sort_order  TINYINT      NOT NULL COMMENT 'Порядок этапа в жизненном цикле',
    is_final    TINYINT(1)   NOT NULL DEFAULT 0 COMMENT 'Конечный статус (заявка закрыта)',
    CONSTRAINT pk_order_statuses PRIMARY KEY (id),
    CONSTRAINT uq_order_statuses_code UNIQUE (code)
) ENGINE = InnoDB COMMENT = 'Статусы заявок';

CREATE TABLE orders (
    id                INT            NOT NULL AUTO_INCREMENT COMMENT 'Номер заявки',
    client_id         INT            NOT NULL COMMENT 'Клиент',
    device_id         INT            NOT NULL COMMENT 'Принятое устройство',
    receptionist_id   INT            NOT NULL COMMENT 'Приёмщик, оформивший заявку',
    master_id         INT            NULL     COMMENT 'Назначенный мастер',
    status_id         TINYINT        NOT NULL DEFAULT 1 COMMENT 'Текущий статус',
    declared_fault    VARCHAR(500)   NOT NULL COMMENT 'Заявленная клиентом неисправность',
    appearance_note   VARCHAR(300)   NULL     COMMENT 'Примечание о внешнем виде при приёме',
    diagnosis         TEXT           NULL     COMMENT 'Заключение мастера по итогам диагностики',
    is_warranty       TINYINT(1)     NOT NULL DEFAULT 0 COMMENT 'Гарантийный ремонт (для клиента бесплатно)',
    warranty_months   TINYINT        NULL     COMMENT 'Гарантийный срок на выполненные работы, мес.',
    client_decision   ENUM ('approved', 'rejected') NULL COMMENT 'Решение клиента по стоимости',
    decision_at       DATETIME       NULL     COMMENT 'Дата решения клиента',
    total_cost        DECIMAL(10, 2) NOT NULL DEFAULT 0.00 COMMENT 'Итоговая стоимость: работы + запчасти',
    due_date          DATE           NOT NULL COMMENT 'Плановый срок готовности',
    created_at        DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Дата приёма',
    ready_at          DATETIME       NULL     COMMENT 'Дата готовности к выдаче',
    issued_at         DATETIME       NULL     COMMENT 'Дата выдачи клиенту',
    updated_at        DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'Дата последнего изменения',
    CONSTRAINT pk_orders PRIMARY KEY (id),
    CONSTRAINT fk_orders_client FOREIGN KEY (client_id) REFERENCES users (id),
    CONSTRAINT fk_orders_device FOREIGN KEY (device_id) REFERENCES devices (id),
    CONSTRAINT fk_orders_receptionist FOREIGN KEY (receptionist_id) REFERENCES users (id),
    CONSTRAINT fk_orders_master FOREIGN KEY (master_id) REFERENCES users (id),
    CONSTRAINT fk_orders_status FOREIGN KEY (status_id) REFERENCES order_statuses (id),
    CONSTRAINT ck_orders_total CHECK (total_cost >= 0),
    CONSTRAINT ck_orders_warranty CHECK (warranty_months IS NULL OR warranty_months BETWEEN 0 AND 36),
    INDEX ix_orders_created (created_at)
) ENGINE = InnoDB AUTO_INCREMENT = 10000 COMMENT = 'Заявки на ремонт';

CREATE TABLE completeness_items (
    id    INT          NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор пункта',
    name  VARCHAR(80)  NOT NULL COMMENT 'Наименование (например, «Зарядное устройство»)',
    kind  ENUM ('accessory', 'appearance') NOT NULL COMMENT 'accessory — комплектующее, appearance — дефект внешнего вида',
    CONSTRAINT pk_completeness_items PRIMARY KEY (id),
    CONSTRAINT uq_completeness_items_name UNIQUE (name)
) ENGINE = InnoDB COMMENT = 'Справочник пунктов комплектности';

CREATE TABLE order_completeness (
    order_id  INT NOT NULL COMMENT 'Заявка',
    item_id   INT NOT NULL COMMENT 'Отмеченный пункт комплектности',
    CONSTRAINT pk_order_completeness PRIMARY KEY (order_id, item_id),
    CONSTRAINT fk_oc_order FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT fk_oc_item FOREIGN KEY (item_id) REFERENCES completeness_items (id)
) ENGINE = InnoDB COMMENT = 'Комплектность устройства при приёме';

-- ---------------------------------------------------------------------
--  Подсистема «Управление ремонтом»
-- ---------------------------------------------------------------------

CREATE TABLE order_status_history (
    id          INT           NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор записи',
    order_id    INT           NOT NULL COMMENT 'Заявка',
    status_id   TINYINT       NOT NULL COMMENT 'Установленный статус',
    changed_by  INT           NOT NULL COMMENT 'Пользователь, сменивший статус',
    changed_at  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Дата и время смены',
    comment     VARCHAR(300)  NULL     COMMENT 'Комментарий к смене статуса',
    CONSTRAINT pk_order_status_history PRIMARY KEY (id),
    CONSTRAINT fk_osh_order FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT fk_osh_status FOREIGN KEY (status_id) REFERENCES order_statuses (id),
    CONSTRAINT fk_osh_user FOREIGN KEY (changed_by) REFERENCES users (id),
    INDEX ix_osh_order (order_id, changed_at)
) ENGINE = InnoDB COMMENT = 'История статусов заявки';

CREATE TABLE services (
    id         INT            NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор услуги',
    name       VARCHAR(120)   NOT NULL COMMENT 'Наименование работы',
    price      DECIMAL(10, 2) NOT NULL COMMENT 'Базовая стоимость, руб.',
    is_active  TINYINT(1)     NOT NULL DEFAULT 1 COMMENT 'Услуга доступна для выбора',
    CONSTRAINT pk_services PRIMARY KEY (id),
    CONSTRAINT uq_services_name UNIQUE (name),
    CONSTRAINT ck_services_price CHECK (price >= 0)
) ENGINE = InnoDB COMMENT = 'Прайс-лист работ';

CREATE TABLE order_works (
    id           INT            NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор записи',
    order_id     INT            NOT NULL COMMENT 'Заявка',
    service_id   INT            NULL     COMMENT 'Услуга из прайс-листа (NULL — произвольная работа)',
    master_id    INT            NOT NULL COMMENT 'Мастер, выполнивший работу',
    description  VARCHAR(300)   NOT NULL COMMENT 'Описание выполненной работы',
    price        DECIMAL(10, 2) NOT NULL COMMENT 'Стоимость работы, руб.',
    created_at   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Дата записи',
    CONSTRAINT pk_order_works PRIMARY KEY (id),
    CONSTRAINT fk_ow_order FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT fk_ow_service FOREIGN KEY (service_id) REFERENCES services (id),
    CONSTRAINT fk_ow_master FOREIGN KEY (master_id) REFERENCES users (id),
    CONSTRAINT ck_ow_price CHECK (price >= 0)
) ENGINE = InnoDB COMMENT = 'Журнал работ по заявке';

-- ---------------------------------------------------------------------
--  Подсистема «Склад запчастей»
-- ---------------------------------------------------------------------

CREATE TABLE parts (
    id                 INT            NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор запчасти',
    sku                VARCHAR(30)    NOT NULL COMMENT 'Артикул',
    name               VARCHAR(150)   NOT NULL COMMENT 'Наименование',
    unit               VARCHAR(10)    NOT NULL DEFAULT 'шт' COMMENT 'Единица измерения',
    price              DECIMAL(10, 2) NOT NULL COMMENT 'Цена для клиента, руб.',
    quantity_on_hand   INT            NOT NULL DEFAULT 0 COMMENT 'Физический остаток на складе',
    quantity_reserved  INT            NOT NULL DEFAULT 0 COMMENT 'Зарезервировано под заявки',
    min_quantity       INT            NOT NULL DEFAULT 0 COMMENT 'Неснижаемый остаток',
    location           VARCHAR(30)    NULL     COMMENT 'Ячейка хранения',
    is_active          TINYINT(1)     NOT NULL DEFAULT 1 COMMENT 'Позиция используется',
    created_at         DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Дата создания карточки',
    CONSTRAINT pk_parts PRIMARY KEY (id),
    CONSTRAINT uq_parts_sku UNIQUE (sku),
    CONSTRAINT ck_parts_qty CHECK (quantity_on_hand >= 0),
    CONSTRAINT ck_parts_reserved CHECK (quantity_reserved >= 0 AND quantity_reserved <= quantity_on_hand),
    CONSTRAINT ck_parts_price CHECK (price >= 0)
) ENGINE = InnoDB COMMENT = 'Номенклатура и остатки запчастей';

CREATE TABLE part_reservations (
    id            INT            NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор резерва',
    order_id      INT            NOT NULL COMMENT 'Заявка',
    part_id       INT            NOT NULL COMMENT 'Запчасть',
    quantity      INT            NOT NULL COMMENT 'Количество',
    price         DECIMAL(10, 2) NOT NULL COMMENT 'Цена на момент резервирования, руб.',
    status        ENUM ('requested', 'reserved', 'issued', 'cancelled') NOT NULL DEFAULT 'requested'
                  COMMENT 'requested — ожидает поступления, reserved — зарезервирована, issued — выдана мастеру, cancelled — отменена',
    requested_by  INT            NOT NULL COMMENT 'Мастер, запросивший запчасть',
    issued_by     INT            NULL     COMMENT 'Кладовщик, выдавший запчасть',
    created_at    DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Дата запроса',
    issued_at     DATETIME       NULL     COMMENT 'Дата выдачи',
    CONSTRAINT pk_part_reservations PRIMARY KEY (id),
    CONSTRAINT fk_pr_order FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT fk_pr_part FOREIGN KEY (part_id) REFERENCES parts (id),
    CONSTRAINT fk_pr_requested FOREIGN KEY (requested_by) REFERENCES users (id),
    CONSTRAINT fk_pr_issued FOREIGN KEY (issued_by) REFERENCES users (id),
    CONSTRAINT ck_pr_qty CHECK (quantity > 0),
    INDEX ix_pr_status (status)
) ENGINE = InnoDB COMMENT = 'Резервирование запчастей под заявки';

CREATE TABLE part_movements (
    id             INT           NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор операции',
    part_id        INT           NOT NULL COMMENT 'Запчасть',
    order_id       INT           NULL     COMMENT 'Заявка, к которой относится операция',
    movement_type  ENUM ('receipt', 'reserve', 'unreserve', 'issue', 'inventory') NOT NULL
                   COMMENT 'receipt — оприходование, reserve/unreserve — резерв и его снятие, issue — выдача (списание), inventory — корректировка',
    quantity       INT           NOT NULL COMMENT 'Количество со знаком: + приход, − расход',
    balance_after  INT           NOT NULL COMMENT 'Физический остаток после операции',
    user_id        INT           NOT NULL COMMENT 'Сотрудник, выполнивший операцию',
    created_at     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Дата и время операции',
    comment        VARCHAR(300)  NULL     COMMENT 'Комментарий (номер накладной и т. п.)',
    CONSTRAINT pk_part_movements PRIMARY KEY (id),
    CONSTRAINT fk_pm_part FOREIGN KEY (part_id) REFERENCES parts (id),
    CONSTRAINT fk_pm_order FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE SET NULL,
    CONSTRAINT fk_pm_user FOREIGN KEY (user_id) REFERENCES users (id),
    INDEX ix_pm_part_date (part_id, created_at)
) ENGINE = InnoDB COMMENT = 'Журнал движения запчастей';

CREATE TABLE inventories (
    id             INT           NOT NULL AUTO_INCREMENT COMMENT 'Номер инвентаризации',
    created_by     INT           NOT NULL COMMENT 'Кладовщик, проводивший инвентаризацию',
    created_at     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Дата проведения',
    comment        VARCHAR(300)  NULL     COMMENT 'Комментарий',
    discrepancies  INT           NOT NULL DEFAULT 0 COMMENT 'Количество позиций с расхождением',
    CONSTRAINT pk_inventories PRIMARY KEY (id),
    CONSTRAINT fk_inv_user FOREIGN KEY (created_by) REFERENCES users (id)
) ENGINE = InnoDB COMMENT = 'Инвентаризации склада';

CREATE TABLE inventory_items (
    id            INT NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор строки',
    inventory_id  INT NOT NULL COMMENT 'Инвентаризация',
    part_id       INT NOT NULL COMMENT 'Запчасть',
    expected_qty  INT NOT NULL COMMENT 'Учётный остаток',
    actual_qty    INT NOT NULL COMMENT 'Фактический остаток',
    difference    INT AS (actual_qty - expected_qty) STORED COMMENT 'Расхождение (вычисляемое поле)',
    CONSTRAINT pk_inventory_items PRIMARY KEY (id),
    CONSTRAINT fk_ii_inventory FOREIGN KEY (inventory_id) REFERENCES inventories (id) ON DELETE CASCADE,
    CONSTRAINT fk_ii_part FOREIGN KEY (part_id) REFERENCES parts (id),
    CONSTRAINT uq_ii_part UNIQUE (inventory_id, part_id),
    CONSTRAINT ck_ii_actual CHECK (actual_qty >= 0)
) ENGINE = InnoDB COMMENT = 'Строки инвентаризационной ведомости';

-- ---------------------------------------------------------------------
--  Подсистема «Расчёты и документы»
-- ---------------------------------------------------------------------

CREATE TABLE payments (
    id           INT            NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор платежа',
    order_id     INT            NOT NULL COMMENT 'Заявка',
    amount       DECIMAL(10, 2) NOT NULL COMMENT 'Сумма, руб.',
    method       ENUM ('cash', 'card', 'sbp') NOT NULL COMMENT 'Способ оплаты: наличные, карта, СБП',
    received_by  INT            NOT NULL COMMENT 'Приёмщик, принявший оплату',
    paid_at      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Дата и время оплаты',
    CONSTRAINT pk_payments PRIMARY KEY (id),
    CONSTRAINT fk_pay_order FOREIGN KEY (order_id) REFERENCES orders (id),
    CONSTRAINT fk_pay_user FOREIGN KEY (received_by) REFERENCES users (id),
    CONSTRAINT ck_pay_amount CHECK (amount > 0)
) ENGINE = InnoDB COMMENT = 'Оплаты по заявкам';

CREATE TABLE documents (
    id          INT          NOT NULL AUTO_INCREMENT COMMENT 'Идентификатор документа',
    order_id    INT          NOT NULL COMMENT 'Заявка',
    doc_type    ENUM ('receipt', 'estimate', 'act', 'issue') NOT NULL
                COMMENT 'receipt — квитанция о приёме, estimate — смета, act — акт выполненных работ, issue — акт выдачи',
    number      VARCHAR(30)  NOT NULL COMMENT 'Номер документа (например, КВ-10231)',
    created_by  INT          NOT NULL COMMENT 'Сотрудник, сформировавший документ',
    created_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Дата формирования',
    CONSTRAINT pk_documents PRIMARY KEY (id),
    CONSTRAINT uq_documents_number UNIQUE (number),
    CONSTRAINT uq_documents_order_type UNIQUE (order_id, doc_type),
    CONSTRAINT fk_doc_order FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT fk_doc_user FOREIGN KEY (created_by) REFERENCES users (id)
) ENGINE = InnoDB COMMENT = 'Реестр сформированных документов';

-- ---------------------------------------------------------------------
--  Представления для отчётов
-- ---------------------------------------------------------------------

CREATE VIEW v_orders_overview AS
SELECT o.id                                                 AS order_id,
       CONCAT(c.last_name, ' ', LEFT(c.first_name, 1), '.') AS client,
       c.phone                                              AS client_phone,
       CONCAT(dt.name, ' ', d.brand, ' ', d.model)          AS device,
       s.name                                               AS status,
       CONCAT(m.last_name, ' ', LEFT(m.first_name, 1), '.') AS master,
       o.total_cost,
       COALESCE((SELECT SUM(p.amount) FROM payments p WHERE p.order_id = o.id), 0) AS paid,
       o.created_at,
       o.due_date,
       (s.is_final = 0 AND o.due_date < CURRENT_DATE)       AS is_overdue
FROM orders o
         JOIN users c ON c.id = o.client_id
         JOIN devices d ON d.id = o.device_id
         JOIN device_types dt ON dt.id = d.device_type_id
         JOIN order_statuses s ON s.id = o.status_id
         LEFT JOIN users m ON m.id = o.master_id;

CREATE VIEW v_parts_stock AS
SELECT p.id,
       p.sku,
       p.name,
       p.quantity_on_hand,
       p.quantity_reserved,
       p.quantity_on_hand - p.quantity_reserved                    AS quantity_available,
       p.min_quantity,
       (p.quantity_on_hand - p.quantity_reserved) < p.min_quantity AS below_minimum
FROM parts p
WHERE p.is_active = 1;
