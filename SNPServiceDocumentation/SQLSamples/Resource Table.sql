USE [EngDb-XS]
GO

/****** Object:  Table [dbo].[HIL-XS-AutoFocous]    Script Date: 7/31/2019 2:19:46 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[HIL-XS-AutoFocous](
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

