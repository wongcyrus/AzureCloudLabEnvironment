# Azure Cloud Lab Environment
[Please read this Microsoft Tech Blog for more details](https://techcommunity.microsoft.com/t5/educator-developer-blog/azure-cloud-lab-environment/ba-p/3251380)

Azure Cloud Lab Environment is aims to facilitate educators using Azure in their teaching. Using Azure, educators can create the tailor-made lab environment for every student, and it is very important during the pandemic as students cannot back to school and they do not have a good PC at home. On the other hand, students need to work on some complicate deployment projects to learn Azure across the semester. Two main problems – the First is the project cost to continue running the project for a few months, and the second there is no check point for students. In case, a student done something wrong in middle of semester, then he must redo everything or just give up the project. As a result, it limits the scale of student lab project exercise.
My working group (students) of Hong Kong Institute of Vocational Education (IVE) has built up the Azure Cloud Lab Environment, and widely adopted in the teaching of IT114115 Higher Diploma in Cloud and Data Centre Administration. 

The focus of Azure Cloud Lab Environment:
1.	Fully utilize Azure Services – allow students to rebuild the lab environments under their Azure subscription and continuous lab exercise. And it works for all types of free Azure subscriptions such Azure for Students, Azure Free trial, Azure Education Hub, …
2.	Cost saving – lab infrastructure creates before lab class starts and destroy after the lab class end.
3.	Automation – It follows Google Calendar schedule.
4.	Easy Deploy – Educator can deploy the solution with Terraform.
5.	Serverless – the whole solution is using Azure Function under consumption plan.
6.	Infrastructure Evolution – All students Azure Infrastructure can keep evolution automatically and let them learn how to build a large project with the lowest cost.

## Architecture
![Alt text](Images/AzureCloudLabEnvironmentOverview.png?raw=true "Title")
The main function of the system is to create and destroy Infrastructure according to class schedule.

**CalenderPollingFunction**

It runs every 5 minutes and check the Google Calendar for upcoming class. When event starts it sends message to start-event queue and when event ends it sends message to end-event queue. The event message includes the name of the lab, GitHubRepo, Branch, and Repeated Times. Repeated Times is calculated by CompletedEvent table historical record. It keeps OnGoingEvent table up-to-date.
 
**StartEventPoisonEventFunction and EndEventPoisonEventFunction**

It sends error details to administrator email and saves the error details to ErrorLog table.

**StudentRegistrationFunction** 

It provides an online registration form for students to submit their Azure Subscription services principal. It prevents the duplication of submission by saving the name of lab and subscription ID in Subscription table, make a call to student subscription to ensure the services principal in Contributor role, and save the student email and services principal data in LabCredential table. Student need to 
az ad sp create-for-rbac --role="Contributor" --scopes="/subscriptions/<Your Subscription ID>"
 
**StartLabEventHandlerFunction and EndLabEventHandlerFunction** 
  
They handle the message of start-event queue and end-event queue respectively. They are very similar, and the only difference is the container starting command – deploy.sh and undeploy.sh. they convert the event message into lab object for common parameters such as lab, GitHubRepo, Branch, and Repeated Times. They query the LabCredential table and get the list of students with services principal data. Each student subscription is handled by one Terraform Container, and pass services principal and other data through environment variables. 1 container group holds 10 containers. They record the creating and deleting activities in Deployment table. 

  
**TerraformContainer**
  
It installs Azure CLI, Python 3.9, Terraform, and Curl and it can access the following variables
   
All containers mounts to containershare file share. Each container has it own folder. The deployment files keeps in the folder before the infrastructure deletion. Since deployment tools such as Terraform need to keep files (state file) for undeployment.
deploy.sh 
undeply.sh
 
**TerraformContainerRegistry**
  
It stores the TerraformContainer and it prevents hitting the rate limit of DockerHub.
  
**CallBackFunction**
  
It provides the https endpoint for the TerraformContainer to callback after deployment and undeployment. It updates the Deployment table and email information to students.
The following example is the output of Terraform.
 
If there is a VM, it can return IP address, username, and password to students.
 
All student subscriptions clean up to save cost.

  
## Lab Environment Evolution
  
Create a repeating event according to the class schedule.
 
There are 2 ways to create a continuous changing Azure Infrastructure.
1.	Create new branch for each lab class such as Lab0, Lab1, Lab2, …  and TerraformContainer checkout the difference branch every lab class.
```
lab.Branch = lab.Branch.Replace("###RepeatedTimes###", lab.RepeatedTimes.ToString());
```
2.	Add conditional deployment logic through 2 environment variables REPEAT_TIMES and TF_VAR_REPEAT_TIMES.

## Source Code
Azure Cloud Lab Environment
https://github.com/wongcyrus/AzureCloudLabEnvironment 
Example AzureCloudLabInfrastructure Repo
https://github.com/wongcyrus/AzureCloudLabInfrastructure 
