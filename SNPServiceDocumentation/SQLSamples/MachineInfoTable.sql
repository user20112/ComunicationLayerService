USE [Pac-LiteDb ]
GO

/****** Object:  Table [dbo].[MachineInfoTable]    Script Date: 7/25/2019 10:10:39 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[MachineInfoTable](
	[MachineID] [int] IDENTITY(1,1) NOT NULL,
	[MachineName] [varchar](20) NULL,
	[Line] [varchar](20) NULL,
	[SNPID] [int] NULL,
	[Theo] [int] NULL,
	[Plant] [varchar](20) NULL,
	[Engineer] [varchar](20) NULL
) ON [PRIMARY]
GO

