USE [EngDb-XS]
GO

/****** Object:  Table [dbo].[HIL-XS-AutoFocousDownTimes]    Script Date: 7/31/2019 2:19:55 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[HIL-XS-AutoFocousDownTimes](
	[Timestamp] [datetime2](7) NULL,
	[MReason] [varchar](255) NULL,
	[UReason] [varchar](255) NULL,
	[NAED] [varchar](20) NULL,
	[MachineID] [int] NULL,
	[StatusCode] [nvarchar](30) NULL,
	[Code] [int] NULL
) ON [PRIMARY]
GO

