USE [EngDb-XS]
GO

/****** Object:  Table [dbo].[HIL-XS-CramDownTimes]    Script Date: 8/27/2019 9:44:01 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[HIL-XS-CramDownTimes](
	[Timestamp] [datetime2](7) NULL,
	[MReason] [varchar](255) NULL,
	[UReason] [varchar](255) NULL,
	[NAED] [varchar](20) NULL,
	[MachineID] [int] NULL,
	[StatusCode] [nvarchar](30) NULL,
	[Code] [int] NULL
) ON [PRIMARY]
GO

