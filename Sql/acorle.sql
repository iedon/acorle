/*
 Navicat Premium Data Transfer

 Source Server         : 172.23.91.1
 Source Server Type    : MySQL
 Source Server Version : 80018
 Source Host           : 172.23.91.1:3306
 Source Schema         : acorle

 Target Server Type    : MySQL
 Target Server Version : 80018
 File Encoding         : 65001

 Date: 07/09/2021 20:36:35
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for config
-- ----------------------------
DROP TABLE IF EXISTS `config`;
CREATE TABLE `config`  (
  `zone` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Zone Key',
  `key` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Configuration Key',
  `hash` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Configuration Hash',
  `context` text CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Configuration',
  `last_modified` datetime(6) NOT NULL COMMENT 'Configuration Last Modified Time',
  PRIMARY KEY (`zone`, `key`) USING BTREE,
  UNIQUE INDEX `idx_config`(`zone`, `key`) USING BTREE COMMENT 'Index for config table'
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for load_balancer
-- ----------------------------
DROP TABLE IF EXISTS `load_balancer`;
CREATE TABLE `load_balancer`  (
  `zone` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Zone Key',
  `service` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Service Key',
  `type` int(10) UNSIGNED NOT NULL COMMENT 'Load Balancer Type',
  PRIMARY KEY (`zone`, `service`) USING BTREE,
  UNIQUE INDEX `idx_load_balancer`(`zone`, `service`) USING BTREE COMMENT 'Index for Load Balancer'
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for service
-- ----------------------------
DROP TABLE IF EXISTS `service`;
CREATE TABLE `service`  (
  `zone` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Zone Key',
  `hash` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Service Entry Hash(hash of key and url)',
  `key` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Service Key',
  `name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Service Display Name',
  `url` text CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Service URL',
  `weight` int(11) NOT NULL COMMENT 'Service Load Balance Weight',
  `is_private` tinyint(1) NOT NULL COMMENT 'Service Is Private',
  `added_time` datetime(6) NOT NULL COMMENT 'Service Added Time',
  `expire_time` datetime(6) NOT NULL COMMENT 'Service Expire Time',
  PRIMARY KEY (`zone`, `hash`) USING BTREE,
  UNIQUE INDEX `idx_service`(`zone`, `hash`) USING BTREE COMMENT 'Index for Services',
  INDEX `idx_service_zone`(`zone`) USING BTREE COMMENT 'Index for column zone of table services',
  INDEX `idx_service_zone_key`(`zone`, `key`) USING BTREE COMMENT 'Index for column zone and key of table services'
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for zone
-- ----------------------------
DROP TABLE IF EXISTS `zone`;
CREATE TABLE `zone`  (
  `key` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Zone Key',
  `name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT 'Zone Friendly Name',
  `description` text CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL COMMENT 'Zone Description',
  `secret` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT 'Zone Registration Secret',
  `max_services` int(10) UNSIGNED NOT NULL COMMENT 'Zone Max Allowed Services',
  `reg_interval_seconds` int(10) UNSIGNED NOT NULL COMMENT 'Zone Service Registration Interval Seconds',
  `rpc_timeout_seconds` int(10) UNSIGNED NOT NULL COMMENT 'Zone RPC Request Timeout Seconds',
  `log_user_request` tinyint(1) NOT NULL COMMENT 'Zone Log User Request',
  PRIMARY KEY (`key`) USING BTREE,
  UNIQUE INDEX `idx_zone_key`(`key`) USING BTREE COMMENT 'Index for key',
  INDEX `idx_zone_name`(`name`) USING BTREE COMMENT 'Index for Name'
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;

SET FOREIGN_KEY_CHECKS = 1;
