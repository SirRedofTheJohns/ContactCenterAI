-- Additive first migration; executed by offline provisioning, never by API startup.
-- Operational state only. Reservation source has its own future B05 database.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.SchemaVersion') IS NULL CREATE TABLE dbo.SchemaVersion(Version int NOT NULL PRIMARY KEY, AppliedAt datetimeoffset NOT NULL);
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=1)
BEGIN
CREATE TABLE dbo.Principal(
 Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId varchar(40) NOT NULL,
 Issuer nvarchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
 Subject nvarchar(128) COLLATE Latin1_General_100_BIN2 NOT NULL, MemberRef varchar(40) NULL,
 AllowedRoles int NOT NULL CHECK(AllowedRoles BETWEEN 0 AND 63), Active bit NOT NULL,
 CONSTRAINT UQ_Principal_Subject UNIQUE(Issuer,Subject), CONSTRAINT UQ_Principal_Tenant UNIQUE(Id,TenantId));
CREATE TABLE dbo.UserSession(
 Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId varchar(40) NOT NULL, PrincipalId uniqueidentifier NULL,
 Roles int NOT NULL CHECK(Roles BETWEEN 0 AND 63), ExpiresAt datetimeoffset NOT NULL, RevokedAt datetimeoffset NULL,
 CONSTRAINT FK_Session_Principal FOREIGN KEY(PrincipalId,TenantId) REFERENCES dbo.Principal(Id,TenantId),
 CONSTRAINT CK_Session_Guest CHECK(PrincipalId IS NOT NULL OR Roles=0));
CREATE TABLE dbo.Conversation(
 Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId varchar(40) NOT NULL, PrincipalId uniqueidentifier NULL, GuestSessionId uniqueidentifier NULL,
 Language varchar(2) NOT NULL CHECK(Language IN ('es','en')), Version bigint NOT NULL CHECK(Version>0), Epoch bigint NOT NULL CHECK(Epoch>0),
 Ownership varchar(20) NOT NULL CHECK(Ownership IN ('AI','HandoffPending','HumanOwned','Closed')), CreatedAt datetimeoffset NOT NULL,
 CONSTRAINT FK_Conversation_Principal FOREIGN KEY(PrincipalId,TenantId) REFERENCES dbo.Principal(Id,TenantId),
 CONSTRAINT FK_Conversation_Guest FOREIGN KEY(GuestSessionId) REFERENCES dbo.UserSession(Id),
 CONSTRAINT UQ_Conversation_Tenant UNIQUE(Id,TenantId),
 CONSTRAINT CK_Conversation_Owner CHECK((PrincipalId IS NULL AND GuestSessionId IS NOT NULL) OR (PrincipalId IS NOT NULL AND GuestSessionId IS NULL)));
CREATE TABLE dbo.Assignment(
 Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId varchar(40) NOT NULL, PrincipalId uniqueidentifier NOT NULL, ConversationId uniqueidentifier NOT NULL,
 StartsAt datetimeoffset NOT NULL, EndsAt datetimeoffset NOT NULL, Revoked bit NOT NULL, CHECK(EndsAt>StartsAt),
 FOREIGN KEY(PrincipalId,TenantId) REFERENCES dbo.Principal(Id,TenantId), FOREIGN KEY(ConversationId,TenantId) REFERENCES dbo.Conversation(Id,TenantId));
CREATE INDEX IX_Assignment_Resource ON dbo.Assignment(ConversationId,PrincipalId,TenantId);
CREATE TABLE dbo.Idempotency(
 ScopeId uniqueidentifier NOT NULL, KeyHash char(64) NOT NULL, PayloadHash char(64) NOT NULL, ConversationId uniqueidentifier NOT NULL,
 PRIMARY KEY(ScopeId,KeyHash), FOREIGN KEY(ConversationId) REFERENCES dbo.Conversation(Id));
CREATE TABLE dbo.Message(
 Id uniqueidentifier NOT NULL PRIMARY KEY, ConversationId uniqueidentifier NOT NULL, ClientMessageId uniqueidentifier NOT NULL,
 SanitizedText nvarchar(4000) NOT NULL, PayloadHash char(64) NOT NULL, TurnId uniqueidentifier NOT NULL, AcceptedVersion bigint NOT NULL,
 CreatedAt datetimeoffset NOT NULL, CONSTRAINT UQ_Message_Client UNIQUE(ConversationId,ClientMessageId),
 FOREIGN KEY(ConversationId) REFERENCES dbo.Conversation(Id));
CREATE TABLE dbo.Inbox(
 Id uniqueidentifier NOT NULL PRIMARY KEY, ConversationId uniqueidentifier NOT NULL, MessageId uniqueidentifier NOT NULL UNIQUE,
 Status varchar(20) NOT NULL CHECK(Status IN ('Pending','Processing','Completed','Failed')), CreatedAt datetimeoffset NOT NULL,
 FOREIGN KEY(MessageId) REFERENCES dbo.Message(Id), FOREIGN KEY(ConversationId) REFERENCES dbo.Conversation(Id));
CREATE TABLE dbo.Audit(
 Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId varchar(40) NOT NULL, SessionId uniqueidentifier NOT NULL,
 ResourceId uniqueidentifier NULL, Code varchar(50) NOT NULL, OccurredAt datetimeoffset NOT NULL);
INSERT dbo.SchemaVersion VALUES(1,SYSUTCDATETIME());
END;
COMMIT;
