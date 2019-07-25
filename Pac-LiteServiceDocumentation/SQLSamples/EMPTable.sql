USE [Pac-LiteDb ]
GO

/****** Object:  Table [dbo].[EMPTable]    Script Date: 7/25/2019 10:10:59 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[EMPTable](
	[Temperature] [decimal](5, 2) NULL,
	[Humidity] [decimal](5, 2) NULL,
	[FlowRate] [decimal](6, 2) NULL,
	[ChangeOver5Seconds] [decimal](5, 2) NULL,
	[TimeStamp] [datetime] NULL,
	[Location] [varchar](50) NULL
) ON [PRIMARY]
GO

