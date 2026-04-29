-- ============================================================
-- IngenIA365ERP — Functions (8)
-- Generated: 2026-03-20
-- Adapted from original SOLIDO functions in DBDefinicion.sql
-- Same business logic, renamed with modern conventions
-- ============================================================

SET NOCOUNT ON;
GO

-- ============================================================
-- 1. FN_LND_CreditRatingByCifin (was: CalificaCifin)
-- Classifies loan by CIFIN rating based on days past due
-- and modality (01=Comercial, 02=Consumo, 03=Vivienda, 05=Microcredito)
-- Returns: '01' (A) through '07' (default)
-- ============================================================
CREATE OR ALTER FUNCTION [dbo].[FN_LND_CreditRatingByCifin]
(
    @DaysPastDue    INT,
    @Modality       VARCHAR(2)
)
RETURNS NVARCHAR(2)
AS
BEGIN
    DECLARE @Rating NVARCHAR(2);
    SET @Rating = N'07';

    -- 01 = Comercial
    IF @Modality = '01'
    BEGIN
        IF @DaysPastDue >= 0 AND @DaysPastDue <= 30
            SET @Rating = N'01'
        ELSE IF @DaysPastDue >= 31 AND @DaysPastDue <= 90
            SET @Rating = N'02'
        ELSE IF @DaysPastDue >= 91 AND @DaysPastDue <= 180
            SET @Rating = N'03'
        ELSE IF @DaysPastDue >= 181 AND @DaysPastDue <= 360
            SET @Rating = N'04'
        ELSE IF @DaysPastDue >= 361
            SET @Rating = N'05';
    END

    -- 02 = Consumo
    IF @Modality = '02'
    BEGIN
        IF @DaysPastDue >= 0 AND @DaysPastDue <= 30
            SET @Rating = N'01'
        ELSE IF @DaysPastDue >= 31 AND @DaysPastDue <= 60
            SET @Rating = N'02'
        ELSE IF @DaysPastDue >= 61 AND @DaysPastDue <= 90
            SET @Rating = N'03'
        ELSE IF @DaysPastDue >= 91 AND @DaysPastDue <= 180
            SET @Rating = N'04'
        ELSE IF @DaysPastDue >= 181
            SET @Rating = N'05';
    END

    -- 03 = Vivienda
    IF @Modality = '03'
    BEGIN
        IF @DaysPastDue >= 0 AND @DaysPastDue <= 60
            SET @Rating = N'01'
        ELSE IF @DaysPastDue >= 61 AND @DaysPastDue <= 150
            SET @Rating = N'02'
        ELSE IF @DaysPastDue >= 151 AND @DaysPastDue <= 360
            SET @Rating = N'03'
        ELSE IF @DaysPastDue >= 361 AND @DaysPastDue <= 540
            SET @Rating = N'04'
        ELSE IF @DaysPastDue >= 541
            SET @Rating = N'05';
    END

    -- 05 = Microcredito
    IF @Modality = '05'
    BEGIN
        IF @DaysPastDue >= 0 AND @DaysPastDue <= 30
            SET @Rating = N'01'
        ELSE IF @DaysPastDue >= 31 AND @DaysPastDue <= 60
            SET @Rating = N'02'
        ELSE IF @DaysPastDue >= 61 AND @DaysPastDue <= 90
            SET @Rating = N'03'
        ELSE IF @DaysPastDue >= 91 AND @DaysPastDue <= 120
            SET @Rating = N'04'
        ELSE IF @DaysPastDue >= 121
            SET @Rating = N'05';
    END

    RETURN @Rating;
END;
GO

-- ============================================================
-- 2. FN_LND_DefaultDaysRating (was: CalifiDiasmora)
-- Rates by days past due and balance.
-- If DaysPastDue = 0: '01' if balance > 0, '05' if balance <= 0.
-- Otherwise ranges from '05' to '09'.
-- ============================================================
CREATE OR ALTER FUNCTION [dbo].[FN_LND_DefaultDaysRating]
(
    @DaysPastDue    INT,
    @Balance        DECIMAL(18,2)
)
RETURNS NVARCHAR(2)
AS
BEGIN
    DECLARE @Rating NVARCHAR(2);
    SET @Rating = N'05';

    IF @DaysPastDue >= 30 AND @DaysPastDue <= 59
        SET @Rating = N'06'
    ELSE IF @DaysPastDue >= 60 AND @DaysPastDue <= 89
        SET @Rating = N'07'
    ELSE IF @DaysPastDue >= 90 AND @DaysPastDue <= 119
        SET @Rating = N'08'
    ELSE IF @DaysPastDue >= 120
        SET @Rating = N'09'
    ELSE IF @DaysPastDue = 0
    BEGIN
        IF @Balance > 0
            SET @Rating = N'01'
        ELSE IF @Balance <= 0
            SET @Rating = N'05';
    END

    RETURN @Rating;
END;
GO

-- ============================================================
-- 3. FN_LND_DefaultAgeRating (was: CalifiEdadMora)
-- Categorizes default age in 30-day buckets.
-- Returns '001' through '360' or '000'.
-- ============================================================
CREATE OR ALTER FUNCTION [dbo].[FN_LND_DefaultAgeRating]
(
    @DaysPastDue    INT
)
RETURNS NVARCHAR(3)
AS
BEGIN
    DECLARE @Rating NVARCHAR(3);
    SET @Rating = N'000';

    IF @DaysPastDue >= 0 AND @DaysPastDue <= 29
        SET @Rating = N'001'
    IF @DaysPastDue >= 30 AND @DaysPastDue <= 59
        SET @Rating = N'030'
    ELSE IF @DaysPastDue >= 60 AND @DaysPastDue <= 89
        SET @Rating = N'060'
    ELSE IF @DaysPastDue >= 90 AND @DaysPastDue <= 119
        SET @Rating = N'090'
    ELSE IF @DaysPastDue >= 120 AND @DaysPastDue <= 149
        SET @Rating = N'120'
    ELSE IF @DaysPastDue >= 150 AND @DaysPastDue <= 179
        SET @Rating = N'150'
    ELSE IF @DaysPastDue >= 180 AND @DaysPastDue <= 209
        SET @Rating = N'180'
    ELSE IF @DaysPastDue >= 210 AND @DaysPastDue <= 239
        SET @Rating = N'210'
    ELSE IF @DaysPastDue >= 240 AND @DaysPastDue <= 269
        SET @Rating = N'240'
    ELSE IF @DaysPastDue >= 270 AND @DaysPastDue <= 299
        SET @Rating = N'270'
    ELSE IF @DaysPastDue >= 300 AND @DaysPastDue <= 329
        SET @Rating = N'300'
    ELSE IF @DaysPastDue >= 330 AND @DaysPastDue <= 359
        SET @Rating = N'330'
    ELSE IF @DaysPastDue >= 360
        SET @Rating = N'360';

    RETURN @Rating;
END;
GO

-- ============================================================
-- 4. FN_LND_DefaultAgeRatingCifin (was: CalifiEdadMoraCifin)
-- CIFIN-specific age rating. Similar buckets but different
-- thresholds at the upper end (540+).
-- Returns '00' through '13'.
-- ============================================================
CREATE OR ALTER FUNCTION [dbo].[FN_LND_DefaultAgeRatingCifin]
(
    @DaysPastDue    INT
)
RETURNS NVARCHAR(3)
AS
BEGIN
    DECLARE @Rating NVARCHAR(3);
    SET @Rating = N'00';

    IF @DaysPastDue >= 0 AND @DaysPastDue <= 29
        SET @Rating = N'00'
    IF @DaysPastDue >= 30 AND @DaysPastDue <= 59
        SET @Rating = N'01'
    ELSE IF @DaysPastDue >= 60 AND @DaysPastDue <= 89
        SET @Rating = N'02'
    ELSE IF @DaysPastDue >= 90 AND @DaysPastDue <= 119
        SET @Rating = N'03'
    ELSE IF @DaysPastDue >= 120 AND @DaysPastDue <= 149
        SET @Rating = N'04'
    ELSE IF @DaysPastDue >= 150 AND @DaysPastDue <= 179
        SET @Rating = N'05'
    ELSE IF @DaysPastDue >= 180 AND @DaysPastDue <= 209
        SET @Rating = N'06'
    ELSE IF @DaysPastDue >= 210 AND @DaysPastDue <= 239
        SET @Rating = N'07'
    ELSE IF @DaysPastDue >= 240 AND @DaysPastDue <= 269
        SET @Rating = N'08'
    ELSE IF @DaysPastDue >= 270 AND @DaysPastDue <= 299
        SET @Rating = N'09'
    ELSE IF @DaysPastDue >= 300 AND @DaysPastDue <= 329
        SET @Rating = N'10'
    ELSE IF @DaysPastDue >= 330 AND @DaysPastDue <= 359
        SET @Rating = N'11'
    ELSE IF @DaysPastDue >= 360 AND @DaysPastDue <= 539
        SET @Rating = N'12'
    ELSE IF @DaysPastDue >= 540
        SET @Rating = N'13';

    RETURN @Rating;
END;
GO

-- ============================================================
-- 5. FN_LND_CifinStatus (was: EstadoCifin)
-- Determines CIFIN status (active '01' vs castoff '04'/'05')
-- by modality and days past due.
-- ============================================================
CREATE OR ALTER FUNCTION [dbo].[FN_LND_CifinStatus]
(
    @DaysPastDue    INT,
    @Modality       VARCHAR(2)
)
RETURNS NVARCHAR(2)
AS
BEGIN
    DECLARE @Status NVARCHAR(2);
    SET @Status = N'01';

    -- 01 = Comercial
    IF @Modality = '01'
    BEGIN
        IF @DaysPastDue >= 361
            SET @Status = N'05'
        ELSE IF @DaysPastDue >= 181 AND @DaysPastDue <= 360
            SET @Status = N'04'
        ELSE
            SET @Status = N'01';
    END

    -- 02 = Consumo
    IF @Modality = '02'
    BEGIN
        IF @DaysPastDue >= 181
            SET @Status = N'05'
        ELSE IF @DaysPastDue >= 91 AND @DaysPastDue <= 180
            SET @Status = N'04'
        ELSE
            SET @Status = N'01';
    END

    -- 03 = Vivienda
    IF @Modality = '03'
    BEGIN
        IF @DaysPastDue >= 541
            SET @Status = N'05'
        ELSE IF @DaysPastDue >= 361 AND @DaysPastDue <= 540
            SET @Status = N'04'
        ELSE
            SET @Status = N'01';
    END

    -- 05 = Microcredito
    IF @Modality = '05'
    BEGIN
        IF @DaysPastDue >= 121
            SET @Status = N'05'
        ELSE IF @DaysPastDue >= 91 AND @DaysPastDue <= 120
            SET @Status = N'04'
        ELSE
            SET @Status = N'01';
    END

    RETURN @Status;
END;
GO

-- ============================================================
-- 6. FN_PAY_AnniversaryYears (was: lustros)
-- Calculates anniversary milestones (lustros) for employees.
-- Returns the number of multiples (@Multiple) of years since
-- join date, but only when the current month matches the join month.
-- Uses DATE instead of SMALLDATETIME.
-- ============================================================
CREATE OR ALTER FUNCTION [dbo].[FN_PAY_AnniversaryYears]
(
    @JoinDate       DATE,
    @Month          INT,
    @Year           INT,
    @Multiple       INT
)
RETURNS INT
AS
BEGIN
    DECLARE @Anniversary INT;
    SET @Anniversary = 0;

    IF @Month = MONTH(@JoinDate)
    BEGIN
        IF (@Year - YEAR(@JoinDate)) % @Multiple = 0
            SET @Anniversary = (@Year - YEAR(@JoinDate)) / @Multiple
        ELSE
            SET @Anniversary = 0;
    END
    ELSE
        SET @Anniversary = 0;

    RETURN @Anniversary;
END;
GO

-- ============================================================
-- 7. FN_PAY_AverageSalary (was: promsalario)
-- Calculates average salary for payroll purposes.
-- If salary <= threshold, includes transport subsidy.
-- Always includes accumulated extras prorated to 30 days.
-- Uses DATE instead of DATETIME.
-- ============================================================
CREATE OR ALTER FUNCTION [dbo].[FN_PAY_AverageSalary]
(
    @Salary             DECIMAL(18,2),
    @TransportSubsidy   DECIMAL(18,2),
    @Accumulated        DECIMAL(18,2),
    @Threshold          DECIMAL(18,2),
    @PeriodStart        DATE,
    @PeriodEnd          DATE
)
RETURNS DECIMAL(18,2)
AS
BEGIN
    DECLARE @AverageSalary  DECIMAL(18,2);
    DECLARE @Days           DECIMAL(18,2);
    DECLARE @DailyAverage   DECIMAL(18,2);

    SET @Days = DATEDIFF(DAY, @PeriodStart, @PeriodEnd) + 1;
    SET @AverageSalary = @Salary;
    SET @DailyAverage = ISNULL((@Accumulated / @Days) * 30, 0);

    IF @Salary <= @Threshold
        SET @AverageSalary = @AverageSalary + @TransportSubsidy + @DailyAverage
    ELSE
        SET @AverageSalary = @AverageSalary + @DailyAverage;

    RETURN @AverageSalary;
END;
GO

-- ============================================================
-- 8. FN_LND_ObligationType (was: TipoObligacion)
-- Determines obligation type and rating letter for FOGACLA reporting.
-- @FogaclaType: '1' = type 1, '2' = type 2, other = type 4.
-- Returns 2-char string: [A-E] + [1|2|4].
-- ============================================================
CREATE OR ALTER FUNCTION [dbo].[FN_LND_ObligationType]
(
    @DaysPastDue    INT,
    @FogaclaType    VARCHAR(1)
)
RETURNS NVARCHAR(2)
AS
BEGIN
    DECLARE @ObligationType NVARCHAR(2);
    DECLARE @Rating         NVARCHAR(1);

    SET @ObligationType = N'A4';

    -- FogaclaType = '1'
    IF @FogaclaType = '1'
    BEGIN
        IF @DaysPastDue >= 0 AND @DaysPastDue <= 30
            SET @Rating = N'A'
        ELSE IF @DaysPastDue >= 31 AND @DaysPastDue <= 60
            SET @Rating = N'B'
        ELSE IF @DaysPastDue >= 61 AND @DaysPastDue <= 90
            SET @Rating = N'C'
        ELSE IF @DaysPastDue >= 91 AND @DaysPastDue <= 180
            SET @Rating = N'D'
        ELSE IF @DaysPastDue >= 181
            SET @Rating = N'E';

        SET @ObligationType = @Rating + N'1';
    END

    -- FogaclaType = '2'
    IF @FogaclaType = '2'
    BEGIN
        IF @DaysPastDue >= 0 AND @DaysPastDue <= 30
            SET @Rating = N'A'
        ELSE IF @DaysPastDue >= 31 AND @DaysPastDue <= 90
            SET @Rating = N'B'
        ELSE IF @DaysPastDue >= 91 AND @DaysPastDue <= 180
            SET @Rating = N'C'
        ELSE IF @DaysPastDue >= 181 AND @DaysPastDue <= 360
            SET @Rating = N'D'
        ELSE IF @DaysPastDue >= 361
            SET @Rating = N'E';

        SET @ObligationType = @Rating + N'2';
    END

    -- FogaclaType <> '1' AND <> '2' (default type 4)
    IF @FogaclaType <> '1' AND @FogaclaType <> '2'
    BEGIN
        IF @DaysPastDue >= 0 AND @DaysPastDue <= 30
            SET @Rating = N'A'
        ELSE IF @DaysPastDue >= 31 AND @DaysPastDue <= 90
            SET @Rating = N'B'
        ELSE IF @DaysPastDue >= 91 AND @DaysPastDue <= 180
            SET @Rating = N'C'
        ELSE IF @DaysPastDue >= 181 AND @DaysPastDue <= 360
            SET @Rating = N'D'
        ELSE IF @DaysPastDue >= 361
            SET @Rating = N'E';

        SET @ObligationType = @Rating + N'4';
    END

    RETURN @ObligationType;
END;
GO

PRINT N'Functions created successfully. Total: 8 scalar functions.';
GO
