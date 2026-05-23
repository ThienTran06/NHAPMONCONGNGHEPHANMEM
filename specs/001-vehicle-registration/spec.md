# Feature Specification: Vehicle Registration

**Feature Branch**: `001-vehicle-registration`

**Created**: 2026-05-18

**Status**: Draft

**Input**: User description: "minh dang build feature vehicles cho app. minh muon giao dien phai hien dai, don gian va hoi sleek mot chut. minh muon co 1 drop down list hien thi so thang dang ky xe may voi items la : 1 2 3 6 9 12, text nho phia tren dropdown la so thang dang ky giu xe. mot textbox de sinh vien dien bien so xe voi format chinh la: XXYX-XXXX hoac XXYX-XXXXX voi X la so, Y la 1 chu cai. cac format bien so sau day chap nhan: XXYXXXXX hoac XXYXXXXXX nhung noi chung se parse ve dang format chinh voi tat ca cac chu in hoa. drop bar de ngang hang voi text box va ngang hang voi 1 button dang ky. phia ben duoi la 1 bang gom 8 cot gom: STT(int), Ngay dang ky(date), Ngay thanh toan(date, null neu chua thanh toan), So tien(money, month * 40000), So thang(int), Bien so xe, Tinh trang(da thanh toan va ap dung hoac da thanh toan hoac chua thanh toan), Ngay het han ve thang(date). ngay ap dung la ngay hom sau so voi ngay thanh toan. bo sung: khi sinh vien bam dang ky, tao invoice trong billing, han dong la 2 ngay, sau 2 ngay sinh vien chua dong thi trang thai invoice trong billing hien thi overdue. o vehicle thi xoa trang thai dang dang ky."

**Project Boundary**: This specification targets the DormitoryManagement WPF desktop
application. Do not introduce ASP.NET Core APIs, REST controllers, JWT flows, or web
assumptions unless the user explicitly requests that scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register motorbike parking (Priority: P1)

A signed-in student registers a motorbike parking pass from the Vehicles screen by selecting
the number of months, entering a license plate, and pressing the registration button.

**Primary Actor**: Student

**Why this priority**: This is the main value of the feature: students can submit a parking
registration without staff entering data for them.

**Independent Test**: A student can choose a valid month option, enter a valid plate in either
official or compact format, register, and see a new unpaid row with the correct amount.

**Acceptance Scenarios**:

1. **Given** a signed-in student is on the Vehicles screen, **When** the student selects
   `3`, enters `59A12345`, and chooses `Đăng ký`, **Then** the system registers `59A1-2345`,
   stores `3` months, sets the amount to `120000`, sets the registration date to today, and
   displays the row as `Chưa thanh toán`, and creates a matching unpaid invoice in Billing
   with a due date two calendar days after registration.
2. **Given** a signed-in student enters `59a1-23456`, **When** registration succeeds, **Then**
   the displayed and stored license plate is normalized to `59A1-23456`.

---

### User Story 2 - Review parking registration status (Priority: P2)

A student reviews their vehicle parking registrations in a simple table that shows the
registration, payment, amount, duration, normalized plate, status, and monthly pass expiry.

**Primary Actor**: Student

**Why this priority**: Students need a clear answer to whether the registration is unpaid,
paid but not active yet, or paid and currently applied.

**Independent Test**: Rows with no payment date, a payment date of today, and a payment date
before today show the correct status and date values.

**Acceptance Scenarios**:

1. **Given** a registration has no payment date, **When** the table is shown, **Then** `Ngày
   thanh toán` is blank, `Tình trạng` is `Chưa thanh toán`, and `Ngày hết hạn vé tháng` is
   blank.
2. **Given** a registration is paid on `2026-05-18` for `1` month, **When** the table is shown
   on `2026-05-18`, **Then** `Tình trạng` is `Đã thanh toán`, the application date is
   `2026-05-19`, and the expiry date is `2026-06-18`.
3. **Given** the same registration is viewed on or after `2026-05-19`, **When** the table is
   shown, **Then** `Tình trạng` is `Đã thanh toán và áp dụng`.
4. **Given** a registration invoice is due on `2026-05-20` and remains unpaid on
   `2026-05-21`, **When** the student opens Billing, **Then** the invoice status is shown as
   overdue.

---

### User Story 3 - Correct invalid registration input (Priority: P3)

A student receives clear validation feedback when the month or license plate input is invalid,
without creating a bad registration row.

**Primary Actor**: Student

**Why this priority**: Plate and payment data must stay clean because active license plates
are unique and drive parking pass state.

**Independent Test**: Invalid plate values are rejected, valid compact values are normalized,
and duplicate active plates are blocked.

**Acceptance Scenarios**:

1. **Given** the student enters `59AA-2345`, **When** they choose `Đăng ký`, **Then** no row is
   created and a validation message explains the accepted formats.
2. **Given** another active registration already uses `59A1-2345`, **When** the student tries
   to register `59a12345`, **Then** no duplicate active registration is created.

### Edge Cases

- License plate input is lowercase, contains leading or trailing spaces, or omits the hyphen.
- License plate input has too few digits, too many digits, more than one letter, or a letter in
  the wrong position.
- The selected duration is missing or not one of `1`, `2`, `3`, `6`, `9`, or `12`.
- A payment date exists but the current date is before the application date.
- A payment date exists and the calculated expiry date has already passed.
- The related Billing invoice is unpaid on the due date or after the due date.
- The student has no vehicle registrations yet.
- The table contains enough registrations to require scrolling.
- A duplicate active license plate exists for another student.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST show a modern, simple, slightly sleek vehicle registration area with
  a month dropdown, license plate textbox, and registration button aligned on one horizontal row.
- **FR-002**: System MUST show the small label `Số tháng đăng ký giữ xe` above the month
  dropdown.
- **FR-003**: The month dropdown MUST contain exactly these selectable values: `1`, `2`, `3`,
  `6`, `9`, and `12`.
- **FR-004**: The license plate textbox MUST accept official formats `XXYX-XXXX` and
  `XXYX-XXXXX`, where `X` is a digit and `Y` is one letter.
- **FR-005**: The license plate textbox MUST also accept compact formats `XXYXXXXX` and
  `XXYXXXXXX`, then normalize them to the official hyphenated format.
- **FR-006**: License plate normalization MUST trim outer spaces, uppercase all letters, and
  display/store the normalized value.
- **FR-007**: System MUST reject license plates that do not match the accepted official or
  compact formats and MUST NOT create a registration row for invalid input.
- **FR-008**: The registration button MUST use the visible label `Đăng ký`.
- **FR-009**: A successful registration MUST create an unpaid vehicle parking registration for
  the signed-in student.
- **FR-010**: A successful registration MUST create a matching unpaid invoice in Billing for
  the same student, license plate, selected month count, and calculated amount.
- **FR-011**: The Billing invoice due date MUST be two calendar days after the vehicle
  registration date.
- **FR-012**: An unpaid vehicle registration invoice MUST show an overdue status in Billing
  when the current date is later than the due date.
- **FR-013**: System MUST calculate `Số tiền` as selected month count multiplied by `40000`.
- **FR-014**: The registration table MUST show these 8 columns in this order: `STT`, `Ngày đăng
  ký`, `Ngày thanh toán`, `Số tiền`, `Số tháng`, `Biển số xe`, `Tình trạng`, `Ngày hết hạn vé
  tháng`.
- **FR-015**: `STT` MUST be a 1-based integer row number in the current table order.
- **FR-016**: `Ngày thanh toán` MUST be blank when the registration has not been paid.
- **FR-017**: The application date MUST be the day after the payment date.
- **FR-018**: `Ngày hết hạn vé tháng` MUST be blank until payment exists, then equal the
  application date plus the selected number of calendar months minus one day.
- **FR-019**: `Tình trạng` MUST be `Chưa thanh toán` when payment date is blank.
- **FR-020**: `Tình trạng` MUST be `Đã thanh toán` when payment date exists and the current
  date is before the application date.
- **FR-021**: `Tình trạng` MUST be `Đã thanh toán và áp dụng` when payment date exists and the
  current date is on or after the application date.
- **FR-022**: The Vehicles screen MUST NOT display a `Đang đăng ký` status; the vehicle table
  status MUST be limited to the requested payment-derived labels.
- **FR-023**: The table MUST show registrations for the signed-in student only.
- **FR-024**: System MUST prevent duplicate active license plate registrations.
- **FR-025**: The screen MUST show useful empty, loading, validation, and denied-access states.

### Authorization and Security Requirements

- **SR-001**: Application Services MUST enforce that students can create and view only their own
  vehicle registrations.
- **SR-002**: UI visibility MUST NOT be the only authorization control.
- **SR-003**: Staff, managers, building managers, and admins MUST NOT be required for a student
  to submit their own registration.
- **SR-004**: The feature MUST NOT add ASP.NET Core API, REST, JWT, or web-auth behavior unless
  explicitly in scope.
- **SR-005**: Invalid input errors MUST be user-friendly and MUST NOT expose internal storage or
  exception details.

### Data and Audit Requirements

- **DR-001**: System MUST preserve active vehicle license plate uniqueness.
- **DR-002**: Currency values MUST use decimal money handling, never floating point.
- **DR-003**: Vehicle registration itself is not added as a new audited domain in this feature;
  payment confirmation remains covered by the existing payment audit behavior when payment state
  is changed.
- **DR-004**: Vehicle registration invoice creation MUST be part of the same successful
  registration outcome so the Vehicles and Billing screens cannot disagree about the unpaid
  amount.
- **DR-005**: Payment date and expiry date MUST remain nullable for unpaid registrations.
- **DR-006**: Existing paid and unpaid registrations MUST remain visible as student history.

### Key Entities

- **Vehicle Parking Registration**: A student's motorbike parking request with registration
  date, optional payment date, amount, month count, normalized license plate, status label, and
  optional expiry date.
- **Billing Invoice**: The unpaid billing record created when a vehicle registration succeeds,
  with amount, due date, payment date, and overdue status when unpaid past due.
- **Student**: The signed-in owner of vehicle registrations; students can view and create only
  their own records.
- **Payment State**: The payment-derived data that determines payment date, application date,
  status label, and expiry date.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A student can complete a valid vehicle parking registration in under 30 seconds.
- **SC-002**: 100% of accepted compact license plate inputs are displayed in uppercase official
  hyphenated format after registration.
- **SC-003**: 100% of invalid license plate inputs from the documented edge cases are rejected
  without creating a registration row.
- **SC-004**: The table displays exactly 8 required columns in the required order.
- **SC-005**: Amount calculations match `month * 40000` for all six allowed month options.
- **SC-006**: Status labels match the payment-date rules for unpaid, paid-not-applied, and
  paid-applied rows.
- **SC-007**: A student cannot view or create vehicle registration rows for another student.
- **SC-008**: 100% of successful vehicle registrations create a matching unpaid Billing invoice
  with the correct due date and amount.
- **SC-009**: 100% of unpaid vehicle registration invoices become overdue in Billing after
  their due date.
- **SC-010**: The Vehicles screen never shows `Đang đăng ký` as a row status.

## Assumptions

- The feature is for motorbike parking registration, so the student-facing form does not need a
  separate vehicle type field.
- The screen uses the signed-in student's identity; students do not manually type a student ID.
- Payment collection or confirmation is handled by the existing billing/payment workflow, but
  this feature creates the initial unpaid Billing invoice.
- The invoice due date is registration date plus two calendar days; overdue begins when the
  current date is later than that due date.
- Expiry date is blank until payment exists because the application date depends on the payment
  date.
- Calendar-month expiry uses application date plus selected months minus one day.
- Expired rows remain visible as history; the requested status labels stay limited to `Chưa
  thanh toán`, `Đã thanh toán`, and `Đã thanh toán và áp dụng`.
