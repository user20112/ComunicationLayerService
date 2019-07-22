USE [Pac-LiteDb ] GO SET ANSI_NULLS ON GO  SET QUOTED_IDENTIFIER ON GO  
CREATE TABLE [dbo].[TestMachineShortTimeStatistics](
	[MachineID] [int] NULL, [Good] [bit] NULL, [Bad] [bit] NULL, [Empty] [bit] NULL, [Attempt] [bit] NULL, [Error1] [bit] NULL, [Error2] [bit] NULL, [Error3] [bit] NULL, [Error4] [bit] NULL, [Other] [bit] NULL, [HeadNumber] [int] NULL, [Theoretical] [int] NULL
) ON [PRIMARY] GO

