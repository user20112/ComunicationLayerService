USE [EngDb-XS]
GO

/****** Object:  Table [dbo].[HIL-XS-CramShortTimeStatistics]    Script Date: 8/27/2019 9:43:52 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[HIL-XS-CramShortTimeStatistics](
	[MachineID] [int] NOT NULL,
	[Timestamp] [datetime2](7) NOT NULL,
	[Good] [bit] NOT NULL,
	[Bad] [bit] NOT NULL,
	[Empty] [bit] NOT NULL,
	[Attempt] [bit] NOT NULL,
	[Input] [bit] NOT NULL,
	[Other] [bit] NOT NULL,
	[Head_number] [int] NOT NULL,
	[Error1] [bit] NOT NULL,
	[Error2] [bit] NOT NULL
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[HIL-XS-CramShortTimeStatistics] ADD  DEFAULT ((0)) FOR [Error1]
GO

ALTER TABLE [dbo].[HIL-XS-CramShortTimeStatistics] ADD  DEFAULT ((0)) FOR [Error2]
GO

