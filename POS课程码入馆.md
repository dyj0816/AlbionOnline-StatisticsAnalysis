# POS课程码入馆

## 查看报名信息

#### 请求地址

```
GET /api/enter/enrollQrCode/getEnrollTrainingInfo
```

#### 请求参数

| 参数名称     | 参数值 | 是否必填 | 备注   |
| ------------ | ------ | -------- | ------ |
| enrollQrcode | string | 是       | 课程码 |


#### 返回值

```
{
    "sysdate": "2024-06-05 16:04:22",
    "custInfo": {
        "custId": 2020102000033602,
        "custName": "马林",
        "custType": "0",
        "custState": "0",
        "psptTypeId": "0",
        "psptId": "320381199312076314",
        "psptAddress": "",
        "venueId": 10000001,
        "inStaffId": 1616,
        "inDate": "2020-10-20 19:04:02",
        "updateTime": "2023-12-29 09:32:10",
        "updateStaffId": 1616,
        "remark": "",
        "openMode": "0",
        "contactPhone": "13151726725",
        "ecardCustId": 2020102000007767,
        "telephone": "",
        "enterpriseName": "",
        "centerId": 10000000,
        "birthday": "1993-12-19 00:00:00",
        "photo": "dev/center/10000000/photo/0816b80187d84e67a6ec936cee3a8338.jpeg",
        "userId": 2020102000034155,
        "acctId": 2020102000033317,
        "ecardId": "E3201000100095840",
        "ecardNo": "E3201000100095840",
        "userRemoveTag": "0",
        "userStatus": "0",
        "userType": "0",
        "birth": false
    },
    "trainingInfo": {
        "lessonNum": 10,
        "trainingType": "2",
        "remainNum": 10,
        "activeTag": "1",
        "endDate": "2024-08-03",
        "suitPersonName": "幼儿",
        "stuName": "马林",
        "studentId": 2020102900003309,
        "courseName": "游泳培训课",
        "custId": 2020102000033602,
        "suitPerson": "1",
        "enrollId": 2024060400009153,
        "privateTag": "0",
        "state": "1",
        "courseId": 10006071,
        "startDate": "2024-06-04"
    },
    "error": 0,
    "message": "ok"
}
```

> trainingInfo.courseName 课程名称
> trainingInfo.stuName 学员名称
>
> trainingInfo.coachName 教练名称
>
> trainingInfo.state 状态值
>
> trainingInfo.courseStateName 状态名称
>
> trainingInfo.remainNum 剩余次数 trainingType为4时是期课 没有剩余次数展示
>
> trainingInfo.startDate trainingInfo.endDate 有效期  

## 查看上课时段

#### 请求地址

```
GET /api/enter/enrollQrCode/checkClassDate
```

#### 请求参数

| 参数名称   | 参数值 | 是否必填 | 备注                       |
| ---------- | ------ | -------- | -------------------------- |
| enrollId   | number | 是       | 课程id                     |
| privateTag | string | 是       | 1-私教；0-培训             |
| classDate  | string | 是       | 选择的签到日期；yyyy-MM-dd |


#### 返回值

```
{
    "sysdate": "2024-06-05 16:32:33",
    "studentInfo": {
        "stuId": 2020102900003309,
        "stuName": "马林",
        "phone": "",
        "psptTypeId": "0",
        "psptId": "",
        "addingTime": "2020-10-29 14:42:24",
        "state": "1",
        "custId": 2020102000033602,
        "birthday": "2022-01-10 00:00:00",
        "selfTag": "1",
        "centerId": 10000000,
        "updateTime": "2022-07-06 14:40:53"
    },
    "dayList": [
        {
            "lessonStart": "1757",
            "id": 501400,
            "lessonEnd": "1758"
        }
    ],
    "courseInfo": {
        "courseId": 10006071,
        "courseName": "游泳培训课",
        "instId": 10000002,
        "courseDesc": "",
        "courseType": 1002,
        "status": "1",
        "startDate": "2024-05-27 00:00:00",
        "endDate": "2027-06-30 00:00:00",
        "lessonNum": 10,
        "price": 1000,
        "privateTag": "0",
        "serviceId": 1005,
        "validPeriod": "60d",
        "trainingType": "2",
        "createTime": "2024-05-27 09:28:47",
        "updateTime": "2024-06-04 14:35:07",
        "updateStaffId": 2109,
        "classTag": "0"
    },
    "tag": true,
    "error": 0,
    "message": "ok"
}
```

> dayList.lessonStart 课程开始时间 
>
> dayList.lessonEnd 课程结束时间 
>
> dayList.recordState 如果不存在改值 说明还没签到 可以选择；1-已上课（不可选择）；2-请假（我也不知道可不可以选择，姑且可选吧）

## 私教上课

#### 请求地址

```
GET /api/enter/enrollQrCode/privateCourseEnter
```

#### 请求参数

| 参数名称 | 参数值 | 是否必填 | 备注       |
| -------- | ------ | -------- | ---------- |
| enrollId | number | 是       | 课程id     |
| lessonId | number | 是       | 课时id     |
| ecardNo  | string | 是       | 一卡通账号 |


#### 返回值

```
{
    "sysdate": "2024-06-05 16:32:33",
    "error": 0,
    "message": "ok"
}
```

## 培训上课

#### 请求地址

```
GET /api/enter/enrollQrCode/courseEnter
```

#### 请求参数

| 参数名称    | 参数值 | 是否必填 | 备注               |
| ----------- | ------ | -------- | ------------------ |
| enrollId    | number | 是       | 课程id             |
| lessonId    | number | 是       | 课时id             |
| ecardNo     | string | 是       | 一卡通账号         |
| lessonDayId | number | 是       | 课时id             |
| classDate   | string | 是       | 上课时间yyyy-MM-dd |
|             |        |          |                    |


#### 返回值

```
{
    "sysdate": "2024-06-05 16:32:33",
    "error": 0,
    "message": "ok"
}
```

## 