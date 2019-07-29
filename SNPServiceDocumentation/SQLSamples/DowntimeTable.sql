USE [StraightBase]
GO

/****** Object:  Table [dbo].[Hil-GS-AutoFocous4DownTimes]    Script Date: 7/29/2019 7:43:15 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Hil-GS-AutoFocous4DownTimes](
	[Timestamp] [datetime2](7) NULL,
	[MReason] [varchar](255) NULL,
	[UReason] [varchar](255) NULL,
	[NAED] [varchar](20) NULL,
	[MachineID] [int] NULL,
	[Status] [nvarchar](30) NULL
) ON [PRIMARY]
GO

