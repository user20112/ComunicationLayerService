USE [EngDb-XS]
GO

/****** Object:  Table [dbo].[HIL-XS-AutoFocousShortTimeStatistics]    Script Date: 7/31/2019 2:20:05 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[HIL-XS-AutoFocousShortTimeStatistics](
	[MachineID] [int] NOT NULL,
	[Timestamp] [datetime2](7) NOT NULL,
	[Good] [bit] NOT NULL,
	[Bad] [bit] NOT NULL,
	[Empty] [bit] NOT NULL,
	[Attempt] [bit] NOT NULL,
	[Other] [bit] NOT NULL,
	[Head_number] [int] NOT NULL,
	[Error1] [bit] NOT NULL,
	[Error2] [bit] NOT NULL,
	[Error3] [bit] NOT NULL,
	[Error4] [bit] NOT NULL,
	[Error5] [bit] NOT NULL,
	[Error6] [bit] NOT NULL,
	[Error7] [bit] NOT NULL,
	[Input] [bit] NULL
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[HIL-XS-AutoFocousShortTimeStatistics] ADD  DEFAULT ((0)) FOR [Error1]
GO

ALTER TABLE [dbo].[HIL-XS-AutoFocousShortTimeStatistics] ADD  DEFAULT ((0)) FOR [Error2]
GO

ALTER TABLE [dbo].[HIL-XS-AutoFocousShortTimeStatistics] ADD  DEFAULT ((0)) FOR [Error3]
GO

ALTER TABLE [dbo].[HIL-XS-AutoFocousShortTimeStatistics] ADD  DEFAULT ((0)) FOR [Error4]
GO

ALTER TABLE [dbo].[HIL-XS-AutoFocousShortTimeStatistics] ADD  DEFAULT ((0)) FOR [Error5]
GO

ALTER TABLE [dbo].[HIL-XS-AutoFocousShortTimeStatistics] ADD  DEFAULT ((0)) FOR [Error6]
GO

ALTER TABLE [dbo].[HIL-XS-AutoFocousShortTimeStatistics] ADD  DEFAULT ((0)) FOR [Error7]
GO

