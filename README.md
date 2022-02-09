# Azure Cloud Lab Environment
Azure Cloud Lab Environment is aims to facilitate educators using Azure in their teaching. Using Azure, educators can create the tailor-made lab environment for every student, and it is very important during the pandemic as students cannot back to school and they do not have a good PC at home. On the other hand, students need to work on some complicate deployment projects to learn Azure across the semester. Two main problems – the First is the project cost to continue running the project for a few months, and the second there is no check point for students. In case, a student done something wrong in middle of semester, then he must redo everything or just give up the project. As a result, it limits the scale of student lab project exercise.
My working group (students) of Hong Kong Institute of Vocational Education (IVE) has built up the Azure Cloud Lab Environment, and widely adopted in the teaching of IT114115 Higher Diploma in Cloud and Data Centre Administration. 

The focus of Azure Cloud Lab Environment:
1.	Fully utilize Azure Services – allow students to rebuild the lab environments under their Azure subscription and continuous lab exercise. And it works for all types of free Azure subscriptions such Azure for Students, Azure Free trial, Azure Education Hub, …
2.	Cost saving – lab infrastructure creates before lab class starts and destroy after the lab class end.
3.	Automation – It follows Google Calendar schedule.
4.	Easy Deploy – Educator can deploy the solution with Terraform.
5.	Serverless – the whole solution is using Azure Function under consumption plan.
6.	Infrastructure Evolution – All students Azure Infrastructure can keep evolution automatically and let them learn how to build a large project with the lowest cost.

