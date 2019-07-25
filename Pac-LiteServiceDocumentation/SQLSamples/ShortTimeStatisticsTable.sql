USE [Pac-LiteDb ]
GO

/****** Object:  Table [dbo].[Hil-GS-AutoFocous4ShortTimeStatistics]    Script Date: 7/25/2019 10:10:22 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Hil-GS-AutoFocous4ShortTimeStatistics](
	[MachineID] [int] NOT NULL,
	[TimeStamp] [date] NOT NULL,
	[Good] [bit] NOT NULL,
	[Bad] [bit] NOT NULL,
	[Empty] [bit] NOT NULL,
	[Attempt] [bit] NOT NULL,
	[Other] [bit] NOT NULL,
	[HeadNumber] [int] NOT NULL,
	[Error1] [bit] NULL,
	[Error2] [bit] NULL
) ON [PRIMARY]
GO

