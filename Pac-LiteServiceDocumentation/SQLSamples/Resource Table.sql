USE [Pac-LiteDb ]
GO

/****** Object:  Table [dbo].[Hil-GS-AutoFocous4]    Script Date: 7/25/2019 10:09:53 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Hil-GS-AutoFocous4](
	[EntryID] [int] IDENTITY(1,1) NOT NULL,
	[MachineID] [int] NULL,
	[Good] [int] NULL,
	[Bad] [int] NULL,
	[Empty] [int] NULL,
	[Indexes] [int] NULL,
	[NAED] [varchar](20) NULL,
	[UOM] [varchar](10) NULL,
	[Time] [datetime] NULL
) ON [PRIMARY]
GO

