USE [Pac-LiteDb ]
GO

/****** Object:  Table [dbo].[Hil-GS-AutoFocous4DownTimes]    Script Date: 7/25/2019 10:10:04 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Hil-GS-AutoFocous4DownTimes](
	[Time] [datetime] NULL,
	[MReason] [varchar](255) NULL,
	[UReason] [varchar](255) NULL,
	[NAED] [varchar](20) NULL,
	[MachineID] [int] NULL,
	[Status] [int] NULL
) ON [PRIMARY]
GO

