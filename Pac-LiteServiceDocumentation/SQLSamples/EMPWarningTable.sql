USE [Pac-LiteDb ]
GO

/****** Object:  Table [dbo].[EMPWarningTable]    Script Date: 7/25/2019 10:10:51 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[EMPWarningTable](
	[Warning] [varchar](255) NULL,
	[TimeStamp] [datetime] NULL,
	[Location] [varchar](50) NULL,
	[Urgency] [int] NULL
) ON [PRIMARY]
GO

