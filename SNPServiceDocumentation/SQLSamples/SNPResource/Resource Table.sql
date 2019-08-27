USE [EngDb-XS]
GO

/****** Object:  Table [dbo].[HIL-XS-Cram]    Script Date: 8/27/2019 9:44:12 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[HIL-XS-Cram](
	[EntryID] [int] IDENTITY(1,1) NOT NULL,
	[MachineID] [int] NULL,
	[Good] [int] NULL,
	[Bad] [int] NULL,
	[Empty] [int] NULL,
	[Indexes] [int] NULL,
	[NAED] [varchar](20) NULL,
	[UOM] [varchar](10) NULL,
	[Timestamp] [datetime2](7) NULL
) ON [PRIMARY]
GO

